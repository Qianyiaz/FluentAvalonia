#pragma warning disable
using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

public class FAItemCollectionTransitionProgress
{
    private readonly WeakReference<FAItemCollectionTransition> _transition;

    public FAItemCollectionTransitionProgress(FAItemCollectionTransition transition)
    {
        _transition = new WeakReference<FAItemCollectionTransition>(transition);
    }

    public Control Element() =>
        _transition.TryGetTarget(out var target) ? target.Element : null;

    public void Complete()
    {
        if (_transition.TryGetTarget(out var target)) target.OwningProvider.NotifyTransitionComplete(target);
    }
}