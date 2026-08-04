using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace MicaForEveryone.Models;

internal static class RuleWindowClassMatcher
{
    public static unsafe bool IsWindowClassListed(HWND hWnd, string? classNames)
    {
        if (string.IsNullOrWhiteSpace(classNames))
        {
            return false;
        }

        char* lpClassName = stackalloc char[256];
        if (GetClassNameW(hWnd, lpClassName, 256) == 0)
        {
            return false;
        }

        ReadOnlySpan<char> className = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(lpClassName);
        ReadOnlySpan<char> listedClassNames = classNames.AsSpan();

        foreach (Range range in listedClassNames.Split([',', ';', '\r', '\n']))
        {
            ReadOnlySpan<char> listedClassName = listedClassNames[range].Trim();
            if (className.Equals(listedClassName, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
