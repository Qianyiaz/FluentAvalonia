using System.Collections;
using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

internal class VirtualLayoutContextAdapter : FANonVirtualizingLayoutContext
{
    private ChildrenCollection _children;

    private WeakReference<FAVirtualizingLayoutContext> _virtualizingContext;

    public VirtualLayoutContextAdapter(FAVirtualizingLayoutContext context)
    {
        _virtualizingContext = new WeakReference<FAVirtualizingLayoutContext>(context);
    }

    protected internal override object LayoutStateCore
    {
        get => GetContext()?.LayoutStateCore;
        set
        {
            if (GetContext() is FAVirtualizingLayoutContext vlc)
                vlc.LayoutStateCore = value;
        }
    }

    protected override IReadOnlyList<Control> ChildrenCore()
    {
        _children ??= new ChildrenCollection(GetContext());
        return _children;
    }

    private FAVirtualizingLayoutContext GetContext() =>
        _virtualizingContext.TryGetTarget(out var target) ? target : null;

    // WinUI makes this Generic, but C# doesn't like the indexer getting a control
    // with returning a generic type
    private class ChildrenCollection : IReadOnlyList<Control>
    {
        private FAVirtualizingLayoutContext _context;

        public ChildrenCollection(FAVirtualizingLayoutContext context)
        {
            _context = context;
        }

        public int Count => _context.ItemCount;

        public Control this[int index]
        {
            get => _context.GetOrCreateElementAt(index, FAElementRealizationOptions.None);
        }

        public IEnumerator<Control> GetEnumerator()
        {
            var ct = Count;
            for (var i = 0; i < ct; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}