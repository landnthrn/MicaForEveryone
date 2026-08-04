using MicaForEveryone.App.Services;
using MicaForEveryone.CoreUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Settings;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using System.Threading.Tasks;
using WinRT;

namespace MicaForEveryone.App;

class Program
{
    internal static bool IsStartupActivation { get; private set; }

    [STAThread]
    public static void Main(string[] _)
    {
        ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirectionAsync().Result == true)
            return;

        XamlOptionalChanges.EnableChange(XamlChangeId.DefaultStyleOptimizations);
        XamlOptionalChanges.EnableChange(XamlChangeId.OptimizeApplyStyles);

        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new MicaForEveryone.App.Dispatching.DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    private static async Task<bool> DecideRedirectionAsync()
    {
        bool isRedirect = false;
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        IsStartupActivation = args.Kind == ExtendedActivationKind.StartupTask;
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey(AppIds.InstanceKey);

        if (keyInstance.IsCurrent)
        {
            AppInstance.GetCurrent().Activated += Program_Activated;
        }
        else
        {
            isRedirect = true;
            await keyInstance.RedirectActivationToAsync(args);
        }
        return isRedirect;
    }

    private static async void Program_Activated(object? sender, AppActivationArguments e)
    {
        if (App.Services.GetService<IDispatchingService>() is IDispatchingService dispatcher)
            await dispatcher.YieldAsync();

        if (e.Kind != ExtendedActivationKind.StartupTask)
            App.Services.GetService<MainAppService>()?.ActivateSettings();
    }
}
