using System.Runtime.CompilerServices;
using FluentAvalonia.Interop;

namespace FluentAvalonia.UI.Windowing;

public partial class FAAppWindow
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitializeAppWindow()
    {
        IsWindows = true;
        IsWindows11 = OSVersionHelper.IsWindows11();

        PseudoClasses.Add(":windows");
        PlatformFeatures = new Win32AppWindowFeatures(this);
    }
}
