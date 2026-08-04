using MicaForEveryone.App.Helpers;
using MicaForEveryone.CoreUI;
using MicaForEveryone.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace MicaForEveryone.App.Services;

// Declare ACCENT_STATE
[Flags]
public enum ACCENT_STATE
{
    ACCENT_DISABLED = 0,
    ACCENT_ENABLE_GRADIENT = 1,
    ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
    ACCENT_ENABLE_BLURBEHIND = 3,
    ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
    ACCENT_ENABLE_HOSTBACKDROP = 5,
    ACCENT_INVALID_STATE = 6
}

// Declare ACCENT_POLICY
[StructLayout(LayoutKind.Sequential)]
public struct ACCENT_POLICY
{
    public ACCENT_STATE AccentState;
    public uint AccentFlags;
    public uint GradientColor;
    public uint AnimationId;
}

// Declare WINDOWCOMPOSITIONATTRIB
public enum WINDOWCOMPOSITIONATTRIB
{
    WCA_ACCENT_POLICY = 19
}

// Declare WINDOWCOMPOSITIONATTRIBDATA
[StructLayout(LayoutKind.Sequential)]
public struct WINDOWCOMPOSITIONATTRIBDATA
{
    public WINDOWCOMPOSITIONATTRIB Attrib;
    public IntPtr pvData;
    public uint cbData;
}

public ref struct Ref<T>
{
    private ref T _reference;

    public Ref(ref T reference)
    {
        _reference = ref reference;
    }

    public ref T GetReference() => ref _reference;
}

public sealed class RuleService : IRuleService
{
    private enum WindowState
    {
        Normal,
        Minimized,
        Maximized
    }

    [DllImport("user32")]
    private static extern BOOL IsTopLevelWindow(HWND hWnd);

    [DllImport("user32")]
    private static unsafe extern BOOL SetWindowCompositionAttribute(HWND hWnd, WINDOWCOMPOSITIONATTRIBDATA* data);

    private readonly ISettingsService _settingsService;
    private readonly IThemingService _themingService;
    private readonly ConcurrentDictionary<HWND, WindowState> _windowStates = new();
    private readonly ConcurrentDictionary<HWND, byte> _windowsWithEffectStateChange = new();
    private HWINEVENTHOOK _showEventHook;
    private HWINEVENTHOOK _minimizeEndEventHook;
    private HWINEVENTHOOK _locationChangeEventHook;
    private HWINEVENTHOOK _destroyEventHook;

    public BackdropType[] SupportedBackdropTypes { get; }

    public RuleService(ISettingsService settingsService, IThemingService themingService)
    {
        _settingsService = settingsService;
        _themingService = themingService;
        _themingService.ThemeChanged += (_, _) => _ = ApplyRulesToAllWindowsAsync();

        if (!AreAdditionalMaterialsSupported)
            SupportedBackdropTypes = [BackdropType.Default, BackdropType.None, BackdropType.Mica];
        else
            SupportedBackdropTypes = Enum.GetValues<BackdropType>();
    }

    Lazy<bool> _is22000 = new(static () => Environment.OSVersion.Version >= new Version(10, 0, 22000));
    Lazy<bool> _is22523 = new(static () => Environment.OSVersion.Version >= new Version(10, 0, 22523));

    public bool AreMaterialsSupported { get => _is22000.Value; }
    public bool AreAdditionalMaterialsSupported { get => _is22523.Value; }
    public bool AreCornerPreferencesSupported { get => _is22000.Value; }

    public unsafe void Initialize()
    {
        _showEventHook = SetWinEventHook(EVENT.EVENT_OBJECT_SHOW, EVENT.EVENT_OBJECT_SHOW, HMODULE.NULL, &NewWindowShown, 0, 0, WINEVENT_OUTOFCONTEXT);

        if (!_is22000.Value)
        {
            _minimizeEndEventHook = SetWinEventHook(EVENT.EVENT_SYSTEM_MINIMIZEEND, EVENT.EVENT_SYSTEM_MINIMIZEEND, HMODULE.NULL, &WindowStateChanged, 0, 0, WINEVENT_OUTOFCONTEXT);
            _locationChangeEventHook = SetWinEventHook(EVENT.EVENT_OBJECT_LOCATIONCHANGE, EVENT.EVENT_OBJECT_LOCATIONCHANGE, HMODULE.NULL, &WindowStateChanged, 0, 0, WINEVENT_OUTOFCONTEXT);
            _destroyEventHook = SetWinEventHook(EVENT.EVENT_OBJECT_DESTROY, EVENT.EVENT_OBJECT_DESTROY, HMODULE.NULL, &WindowDestroyed, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        _settingsService.PropertyChanged += _settingsService_PropertyChanged;
    }

    private void _settingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = ApplyRulesToAllWindowsAsync();
    }

    [UnmanagedCallersOnly]
    private static void NewWindowShown(HWINEVENTHOOK handler, uint winEvent, HWND hWnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        static async Task NewWindowShowHandlerAsync(RuleService service, HWND hwnd)
        {
            if (!IsWindowEligible(hwnd))
                await Task.Delay(10);

            service.RememberWindowState(hwnd);
            await service.ApplyRuleToWindowAsync(hwnd);
        }

        _ = NewWindowShowHandlerAsync((RuleService)App.Services.GetRequiredService<IRuleService>(), hWnd).ConfigureAwait(false);
    }

    [UnmanagedCallersOnly]
    private static void WindowStateChanged(HWINEVENTHOOK handler, uint winEvent, HWND hWnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        const int ObjIdWindow = 0;
        const int ChildIdSelf = 0;

        if (hWnd == HWND.NULL)
            return;

        if (winEvent == (uint)EVENT.EVENT_OBJECT_LOCATIONCHANGE && (idObject != ObjIdWindow || idChild != ChildIdSelf))
            return;

        RuleService service = (RuleService)App.Services.GetRequiredService<IRuleService>();
        WindowState currentState = GetWindowState(hWnd);
        bool hadPreviousState = service._windowStates.TryGetValue(hWnd, out WindowState previousState);
        service._windowStates[hWnd] = currentState;

        if (currentState == WindowState.Minimized)
            return;

        if (winEvent == (uint)EVENT.EVENT_OBJECT_LOCATIONCHANGE && (!hadPreviousState || previousState == currentState || previousState == WindowState.Minimized))
            return;

        if (!service.ShouldReapplyAfterWindowStateChange(hWnd))
            return;

        bool isFirstEffectStateChange = service._windowsWithEffectStateChange.TryAdd(hWnd, 0);
        bool reapplyAfterSettled = isFirstEffectStateChange && currentState == WindowState.Maximized;
        _ = service.ReapplyAfterWindowStateChangeAsync(hWnd, reapplyAfterSettled).ConfigureAwait(false);
    }

    [UnmanagedCallersOnly]
    private static void WindowDestroyed(HWINEVENTHOOK handler, uint winEvent, HWND hWnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        const int ObjIdWindow = 0;
        const int ChildIdSelf = 0;

        if (hWnd != HWND.NULL && idObject == ObjIdWindow && idChild == ChildIdSelf)
        {
            RuleService service = (RuleService)App.Services.GetRequiredService<IRuleService>();
            service._windowStates.TryRemove(hWnd, out _);
            service._windowsWithEffectStateChange.TryRemove(hWnd, out _);
        }
    }

    private void CallEnumWindows()
    {
        RuleService currentRuleService = this;
        Ref<RuleService> ruleService = new(ref currentRuleService);

        unsafe
        {
            EnumWindows(&EnumWindowsProc, new((nint)Unsafe.AsPointer(ref ruleService)));
        }
    }

    public async Task ApplyRulesToAllWindowsAsync()
    {
        // Switch to a background thread, if we are not already in one.
        await TaskScheduler.Default;

        // Increase the session count to prevent concurrency issues,
        // that is, if the user changes the settings while we are applying the rules.
        // This tells the existing procedure to cancel the existing operation.
        // int incrementedValue = Interlocked.Increment(ref _currentSession);

        CallEnumWindows();
    }

    [UnmanagedCallersOnly]
    private static BOOL EnumWindowsProc(HWND hWnd, LPARAM lParam)
    {
        /*
        if (Volatile.Read(ref _currentSession) != lParam.Value.ToInt32())
            // User changed the settings, cancel the operation.
            return BOOL.FALSE;
        */

        unsafe
        {
            RuleService service = Unsafe.AsRef<Ref<RuleService>>(lParam).GetReference();
            service.RememberWindowState(hWnd);
            service.ApplyRuleToWindowAsync(hWnd);
        }

        return BOOL.TRUE;
    }
    
    private static unsafe bool IsWindowEligible(HWND hWnd)
    {
        if (!IsWindowVisible(hWnd))
            return false;

        nint styleEx = GetWindowLongPtrW(hWnd, GWL.GWL_EXSTYLE);

        nint style = GetWindowLongPtrW(hWnd, GWL.GWL_STYLE);

        if ((styleEx & WS.WS_EX_NOACTIVATE) == WS.WS_EX_NOACTIVATE || (styleEx & WS.WS_EX_TRANSPARENT) == WS.WS_EX_TRANSPARENT)
            return false;

        if (IsTopLevelWindow(hWnd) == BOOL.FALSE)
            return false;

        bool hasTitleBar = (style & WS.WS_BORDER) == WS.WS_BORDER && (style & WS.WS_DLGFRAME) == WS.WS_DLGFRAME;

        if ((styleEx & WS.WS_EX_TOOLWINDOW) == WS.WS_EX_TOOLWINDOW && !hasTitleBar)
            return false;

        if ((style & WS.WS_POPUP) == WS.WS_POPUP & !hasTitleBar)
            return false;

        return true;
    }

    private static WindowState GetWindowState(HWND hWnd)
    {
        if (IsIconic(hWnd))
            return WindowState.Minimized;

        return IsZoomed(hWnd) ? WindowState.Maximized : WindowState.Normal;
    }

    private void RememberWindowState(HWND hWnd)
    {
        if (hWnd != HWND.NULL && IsTopLevelWindow(hWnd))
            _windowStates[hWnd] = GetWindowState(hWnd);
    }

    private bool ShouldReapplyAfterWindowStateChange(HWND hWnd)
    {
        Rule? rule = GetMostApplicableRule(hWnd);

        return rule is not null && (rule.ExtendFrameIntoClientArea || rule.EnableBlurBehind);
    }

    private async Task ReapplyAfterWindowStateChangeAsync(HWND hWnd, bool reapplyAfterSettled)
    {
        if (!IsWindowEligible(hWnd))
            await Task.Delay(10);

        if (!IsWindowEligible(hWnd) || !ShouldReapplyAfterWindowStateChange(hWnd))
            return;

        await ApplyRuleToWindowAsync(hWnd);

        if (!reapplyAfterSettled)
            return;

        await Task.Delay(10);

        if (GetWindowState(hWnd) == WindowState.Maximized && IsWindowEligible(hWnd) && ShouldReapplyAfterWindowStateChange(hWnd))
            await ApplyRuleToWindowAsync(hWnd);
    }

    public Task ApplyRuleToWindowAsync(HWND hWnd)
    {
        if (!IsWindowEligible(hWnd))
            return Task.CompletedTask;

        Rule? mostApplicableRule = GetMostApplicableRule(hWnd);

        if (mostApplicableRule is null)
        {
            ClearRuleFromWindow(hWnd);
            return Task.CompletedTask;
        }

        unsafe
        {
            const uint DWMWA_CAPTION_COLOR = 35;
            switch (mostApplicableRule.TitleBarColor)
            {
                case TitleBarColorMode.System:
                case TitleBarColorMode.Light:
                case TitleBarColorMode.Dark:
                    const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
                    TitleBarColorMode normalizedTitleBarColorMode = mostApplicableRule.TitleBarColor == TitleBarColorMode.System ? _themingService.IsDarkMode() ? TitleBarColorMode.Dark : TitleBarColorMode.Light : mostApplicableRule.TitleBarColor;
                    uint useImmersiveDarkMode = (uint)(normalizedTitleBarColorMode == TitleBarColorMode.Dark ? 1 : 0);
                    DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &useImmersiveDarkMode, sizeof(uint));
                    uint defaultCaption = DWMWA_COLOR_DEFAULT;
                    DwmSetWindowAttribute(hWnd, DWMWA_CAPTION_COLOR, &defaultCaption, sizeof(uint));
                    break;
                case TitleBarColorMode.Custom:
                    Windows.UI.Color color = ColorConverter.ConvertToColor(mostApplicableRule.TitleBarColorCode);
                    COLORREF colorref = RGB(color.R, color.G, color.B);
                    DwmSetWindowAttribute(hWnd, DWMWA_CAPTION_COLOR, &colorref, (uint)sizeof(COLORREF));
                    break;
            }
        }

        if (mostApplicableRule.BackdropPreference != BackdropType.Default)
        {
            uint bp = (uint)mostApplicableRule.BackdropPreference;
            unsafe
            {
                if (AreAdditionalMaterialsSupported)
                {
                    const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;
                    DwmSetWindowAttribute(hWnd, DWMWA_SYSTEMBACKDROP_TYPE, &bp, sizeof(uint));
                }
                else
                {
                    const uint DWMWA_MICA_EFFECT = 1029;
                    int micaValue = mostApplicableRule.BackdropPreference == BackdropType.Mica ? 1 : 0;
                    DwmSetWindowAttribute(hWnd, DWMWA_MICA_EFFECT, &micaValue, sizeof(int));
                }
            }
        }

        if (mostApplicableRule.CornerPreference != CornerPreference.Default)
        {
            uint cp = (uint)mostApplicableRule.CornerPreference;
            unsafe
            {
                const uint DWMWA_CORNER_PREFERENCE = 33;
                DwmSetWindowAttribute(hWnd, DWMWA_CORNER_PREFERENCE, &cp, sizeof(uint));
            }
        }

        if (mostApplicableRule.ExtendFrameIntoClientArea)
        {
            const int FullClientAreaMargin = 32767;
            int margin = _is22000.Value ? -1 : FullClientAreaMargin;
            MARGINS margins = new() { cxLeftWidth = margin, cxRightWidth = margin, cyTopHeight = margin, cyBottomHeight = margin };
            unsafe
            {
                DwmExtendFrameIntoClientArea(hWnd, &margins);
            }
        }

        if (mostApplicableRule.EnableBlurBehind)
        {
            unsafe
            {
                DWM_BLURBEHIND bb = new()
                {
                    fEnable = BOOL.TRUE,
                    dwFlags = DWM.DWM_BB_ENABLE,
                    fTransitionOnMaximized = BOOL.FALSE,
                    hRgnBlur = HRGN.NULL
                };

                DwmEnableBlurBehindWindow(hWnd, &bb);

                ACCENT_POLICY accent = new()
                {
                    AccentState = ACCENT_STATE.ACCENT_ENABLE_BLURBEHIND | ACCENT_STATE.ACCENT_ENABLE_GRADIENT,
                    GradientColor = unchecked((uint)((152 << 24) | (0x2B2B2B & 0xFFFFFF)))
                };
                WINDOWCOMPOSITIONATTRIBDATA attrib = new()
                {
                    Attrib = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                    pvData = (nint)(&accent),
                    cbData = (uint)sizeof(ACCENT_POLICY)
                };
                SetWindowCompositionAttribute(hWnd, &attrib);
            }
        }

        return Task.CompletedTask;
    }

    private Rule? GetMostApplicableRule(HWND hWnd)
    {
        Rule[] rules = _settingsService.Settings!.Rules.ToArray();

        bool isExcludedByProcessRule = rules
            .OfType<ProcessRule>()
            .Any(rule => rule.IsProcessMatch(hWnd) && rule.IsWindowClassExcluded(hWnd));

        Rule? mostApplicableRule = rules
            .Where(rule => rule.IsRuleApplicable(hWnd))
            .OrderByDescending(rule => rule.Priority)
            .FirstOrDefault();

        if (isExcludedByProcessRule && mostApplicableRule is GlobalRule)
        {
            return null;
        }

        return mostApplicableRule;
    }

    private void ClearRuleFromWindow(HWND hWnd)
    {
        unsafe
        {
            const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            uint useImmersiveDarkMode = 0;
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &useImmersiveDarkMode, sizeof(uint));

            const uint DWMWA_CAPTION_COLOR = 35;
            uint defaultCaption = DWMWA_COLOR_DEFAULT;
            DwmSetWindowAttribute(hWnd, DWMWA_CAPTION_COLOR, &defaultCaption, sizeof(uint));

            if (AreAdditionalMaterialsSupported)
            {
                const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;
                uint defaultBackdrop = (uint)BackdropType.Default;
                DwmSetWindowAttribute(hWnd, DWMWA_SYSTEMBACKDROP_TYPE, &defaultBackdrop, sizeof(uint));
            }
            else
            {
                const uint DWMWA_MICA_EFFECT = 1029;
                int micaValue = 0;
                DwmSetWindowAttribute(hWnd, DWMWA_MICA_EFFECT, &micaValue, sizeof(int));
            }

            MARGINS margins = default;
            DwmExtendFrameIntoClientArea(hWnd, &margins);

            DWM_BLURBEHIND bb = new()
            {
                fEnable = BOOL.FALSE,
                dwFlags = DWM.DWM_BB_ENABLE,
                fTransitionOnMaximized = BOOL.FALSE,
                hRgnBlur = HRGN.NULL
            };
            DwmEnableBlurBehindWindow(hWnd, &bb);

            ACCENT_POLICY accent = new()
            {
                AccentState = ACCENT_STATE.ACCENT_DISABLED
            };
            WINDOWCOMPOSITIONATTRIBDATA attrib = new()
            {
                Attrib = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                pvData = (nint)(&accent),
                cbData = (uint)sizeof(ACCENT_POLICY)
            };
            SetWindowCompositionAttribute(hWnd, &attrib);
        }
    }
}
