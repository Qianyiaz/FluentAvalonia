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
    
    [LibraryImport(s_ole32)]
    public static unsafe partial HRESULT CoCreateInstance(Guid* rclsid, void* pUnkOuter,
        int dwClsContext, Guid* riid, void** ppv);

    internal static unsafe T CreateInstance<T>(Guid clsid, Guid iid) where T : IUnknown
    {
        void* pUnk;
        var hresult = CoCreateInstance(&clsid, null, 1, &iid, &pUnk);
        if (hresult != 0)
        {
            throw new COMException("CreateInstance", hresult);
        }

        using var unk = MicroComRuntime.CreateProxyFor<IUnknown>(pUnk, true);
        return MicroComRuntime.QueryInterface<T>(unk);
    }
    
    public static readonly Guid ITaskBarList3CLSID = Guid.Parse("56FDF344-FD6D-11D0-958A-006097C9A090");
    public static readonly Guid ITaskBarList3IID = Guid.Parse("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf");
    
    private const string s_ole32 = "ole32.dll";
    private const string s_user32 = "user32.dll";
}