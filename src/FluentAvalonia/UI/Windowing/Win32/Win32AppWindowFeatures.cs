using Avalonia.Logging;
using FluentAvalonia.Interop.WinRT;
using static FluentAvalonia.Interop.Win32Interop;

namespace FluentAvalonia.UI.Windowing;

internal class Win32AppWindowFeatures : IFAAppWindowPlatformFeatures
{
    public Win32AppWindowFeatures(FAAppWindow owner)
    {
        _owner = owner;
    }

    public void SetTaskBarProgressBarState(FATaskBarProgressBarState state)
    {
        if (_taskBarList == null)
        {
            CreateTaskBarList();

            // If creation fails, return 
            if (_taskBarList == null)
                return;
        }

        // Enum values are mapped to TMPF_ from Win32
        _taskBarList.SetProgressState(_owner.TryGetPlatformHandle().Handle, (int)state);
    }

    public void SetTaskBarProgressBarValue(ulong currentValue, ulong totalValue)
    {
        if (_taskBarList == null)
        {
            CreateTaskBarList();

            // If creation fails, return 
            if (_taskBarList == null)
                return;
        }

        _taskBarList.SetProgressValue(_owner.TryGetPlatformHandle().Handle, currentValue, totalValue);
    }

    private void CreateTaskBarList()
    {
        try
        {
            _taskBarList = CreateInstance<ITaskbarList3>(
                ITaskBarList3CLSID, ITaskBarList3IID);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Debug, "AppWindow")?
                    .Log("SetWindowBorderColor", "Unable to create instance of ITaskbarList3", ex);
        }
    }

    private FAAppWindow _owner;
    private ITaskbarList3 _taskBarList;
}
