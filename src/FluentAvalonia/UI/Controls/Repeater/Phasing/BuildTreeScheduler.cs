using Avalonia.Threading;

namespace FluentAvalonia.UI.Controls;

internal static class BuildTreeScheduler
{
    private static readonly double _budgetInMs = 40;
    private static readonly object _lockObj = new();

    [ThreadStatic] private static readonly QPCTimer _timer = new();

    [ThreadStatic] private static readonly List<WorkInfo> _pendingWork = [];

    private static bool _renderingToken;

    public static void RegisterWork(int priority, Action workFunc)
    {
        if (priority < 0)
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be >= 0");
        ArgumentNullException.ThrowIfNull(workFunc);

        QueueTick();
        _pendingWork.Add(new WorkInfo(priority, workFunc));
    }

    public static bool ShouldYield() =>
        _timer.DurationInMilliseconds() > _budgetInMs;

    public static void OnRendering()
    {
        var budgetReached = ShouldYield();
        if (!budgetReached && _pendingWork.Count > 0)
        {
            // Sort in descending order of priority and work from the end of the list to avoid moving around during erase.
            _pendingWork.Sort((x, y) => x.Priority > y.Priority ? 1 : -1);

            var currentIndex = _pendingWork.Count - 1;
            do
            {
                _pendingWork[currentIndex].InvokeWorkFunc();
                _pendingWork.RemoveAt(currentIndex);
            } while (--currentIndex >= 0 && !ShouldYield());
        }

        if (_pendingWork.Count == 0)
            // No more pending work, unhook from rendering event since being hooked up will cause wux to try to 
            // call the event at 60 frames per second
            _renderingToken = false;
        //CompositionTarget.Rendering -= OnRendering;
        // RepeaterTestHooks.NotifyBuildTreeCompleted();
        // Reset the timer so it snaps the time just before rendering
        _timer.Reset();

        // NO CompositionTarget.Rendering event, we need to manually trigger another update as long as we have
        // _renderingToken == true
        if (_renderingToken)
            Dispatcher.UIThread.Post(OnRendering, DispatcherPriority.Render);
    }

    public static void QueueTick()
    {
        if (!_renderingToken)
        {
            _renderingToken = true;
            Dispatcher.UIThread.Post(OnRendering, DispatcherPriority.Render);
        }
    }
}

internal struct WorkInfo
{
    public WorkInfo(int priority, Action workFunc)
    {
        Priority = priority;
        _workFunc = workFunc;
    }

    public int Priority { get; }

    public void InvokeWorkFunc() => _workFunc.Invoke();

    private readonly Action _workFunc;
}