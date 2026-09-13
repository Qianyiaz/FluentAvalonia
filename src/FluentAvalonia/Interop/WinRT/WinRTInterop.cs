using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MicroCom.Runtime;

namespace FluentAvalonia.Interop.WinRT;

internal static partial class WinRTInterop
{
    private static bool _initialized;

    [LibraryImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr hstring);

    private static IntPtr WindowsCreateString(string sourceString)
    {
        ArgumentNullException.ThrowIfNull(sourceString);

        var hr = WindowsCreateString(sourceString, (uint)sourceString.Length, out var hstring);
        if (hr < 0)
            throw new InvalidOperationException($"WindowsCreateString failed with HRESULT: 0x{hr:X8}");
        return hstring;
    }

    [LibraryImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe partial void WindowsDeleteString(IntPtr hString);

    internal static T CreateInstance<T>(string fullName) where T : IUnknown
    {
        var s = WindowsCreateString(fullName);
        EnsureRoInitialized();
        var hr = RoActivateInstance(s, out var pUnk);
        if (hr < 0)
        {
            WindowsDeleteString(s);
            throw new COMException("RoActivateInstance failed", hr);
        }

        using var unk = MicroComRuntime.CreateProxyFor<IUnknown>(pUnk, true);
        WindowsDeleteString(s);
        return unk.QueryInterface<T>();
    }

    private static void EnsureRoInitialized()
    {
        if (_initialized)
            return;
        var hr = RoInitialize(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? RO_INIT_TYPE.RO_INIT_SINGLETHREADED
            : RO_INIT_TYPE.RO_INIT_MULTITHREADED);
        if (hr < 0)
            throw new InvalidOperationException($"RoInitialize failed with HRESULT: 0x{hr:X8}");
        _initialized = true;
    }

    [LibraryImport("combase.dll")]
    private static partial int RoInitialize(RO_INIT_TYPE initType);

    [LibraryImport("combase.dll")]
    private static partial int RoActivateInstance(
        IntPtr activatableClassId,
        out IntPtr instance);

    internal enum RO_INIT_TYPE
    {
        RO_INIT_SINGLETHREADED = 0, // Single-threaded application
        RO_INIT_MULTITHREADED = 1 // COM calls objects on any thread.
    }
}