using System.Diagnostics;
using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

internal class UniqueIdElementPool
{
    private readonly Dictionary<string, Control> _elementMap = new();

    private readonly FAItemsRepeater _owner;

    public UniqueIdElementPool(FAItemsRepeater ir)
    {
        _owner = ir;
        // ItemsRepeater is not fully constructed yet. Don't interact with it.
    }

    public void Add(Control element)
    {
        Debug.Assert(_owner.ItemsSourceView.HasKeyIndexMapping);

        var virtInfo = FAItemsRepeater.GetVirtualizationInfo(element);
        var key = virtInfo.UniqueId;

        if (_elementMap.ContainsKey(key)) throw new InvalidOperationException("The ID is not unique");

        _elementMap.Add(key, element);
    }

    public Control Remove(int index)
    {
        Debug.Assert(_owner.ItemsSourceView.HasKeyIndexMapping);

        Control element = null;
        var key = _owner.ItemsSourceView.KeyFromIndex(index);
        if (_elementMap.TryGetValue(key, out element)) _elementMap.Remove(key);

        return element;
    }

    public void Clear() => _elementMap.Clear();

    public IEnumerator<KeyValuePair<string, Control>> GetEnumerator() => _elementMap.GetEnumerator();
}