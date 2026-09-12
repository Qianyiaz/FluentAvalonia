using System.Runtime.InteropServices;
using FluentAvalonia.Interop.Win32;
using MicroCom.Runtime;

namespace FluentAvalonia.Interop;

internal static partial class Win32Interop
{
    // Re-introduced SystemMetrics P/Invokes used by fallbacks
    [LibraryImport(s_user32, SetLastError = true)]
    private static partial int GetSystemMetrics(int smIndex);

    [LibraryImport(s_user32, SetLastError = true)]
    private static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);
    
    public static int GetSystemMetricsWithFallback(int nIndex, uint dpi)
    {
        if (OSVersionHelper.IsAtLeastWindows10_1607())
            return GetSystemMetricsForDpi(nIndex, dpi);
        return GetSystemMetrics(nIndex);
    }

    private const string s_user32 = "user32.dll";
}