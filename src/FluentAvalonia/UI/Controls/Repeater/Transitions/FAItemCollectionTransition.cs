#pragma warning disable
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

public class FAItemCollectionTransition
{
    private Rect _newBounds;
    private Rect _oldBounds;

    private WeakReference<FAItemCollectionTransitionProvider> _owningProvider;
    private FAItemCollectionTransitionProgress _progress;

    public FAItemCollectionTransition(FAItemCollectionTransitionProvider provider, Control element,
        FAItemCollectionTransitionOperation operation, FAItemCollectionTransitionTriggers triggers)
        : this(provider, element, operation, triggers, default, default)
    {
        Debug.Assert(operation != FAItemCollectionTransitionOperation.Move);
    }

    public FAItemCollectionTransition(FAItemCollectionTransitionProvider provider, Control element,
        FAItemCollectionTransitionTriggers triggers,
        Rect oldBounds, Rect newBounds)
        : this(provider, element, FAItemCollectionTransitionOperation.Move, triggers,
            oldBounds, newBounds)
    {
    }

    public FAItemCollectionTransition(FAItemCollectionTransitionProvider provider, Control element,
        FAItemCollectionTransitionOperation operation, FAItemCollectionTransitionTriggers triggers,
        Rect oldBounds, Rect newBounds)
    {
        _owningProvider = new WeakReference<FAItemCollectionTransitionProvider>(provider);
        Element = element;
        Operation = operation;
        Triggers = triggers;
        _oldBounds = oldBounds;
        _newBounds = newBounds;
    }

    public FAItemCollectionTransitionProvider OwningProvider =>
        _owningProvider.TryGetTarget(out var target) ? target : null;

    public Control Element { get; }

    public bool HasStarted => _progress != null;

    public FAItemCollectionTransitionOperation Operation { get; }

    public FAItemCollectionTransitionTriggers Triggers { get; }

    public FAItemCollectionTransitionProgress Start()
    {
        _progress ??= new FAItemCollectionTransitionProgress(this);

        return _progress;
    }
}