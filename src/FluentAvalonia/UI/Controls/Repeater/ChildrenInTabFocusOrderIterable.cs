using System.Collections;
using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

internal class ChildrenInTabFocusOrderIterable : IEnumerable<Control>
{
    private readonly FAItemsRepeater _repeater;

    public ChildrenInTabFocusOrderIterable(FAItemsRepeater owner)
    {
        _repeater = owner;
    }

    public IEnumerator<Control> GetEnumerator() =>
        new ChildrenInTabFocusOrderIterator(_repeater);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private struct ChildrenInTabFocusOrderIterator : IEnumerator<Control>
    {
        public ChildrenInTabFocusOrderIterator(FAItemsRepeater repeater)
        {
            var children = repeater.Children;
            _realizedChildren = new List<KeyValuePair<int, Control>>(children.Count);

            for (var i = 0; i < children.Count; i++)
            {
                var element = children[i];
                var vInfo = FAItemsRepeater.GetVirtualizationInfo(element);
                if (vInfo.IsRealized) _realizedChildren.Add(new KeyValuePair<int, Control>(vInfo.Index, element));
            }
        }

        public Control Current
        {
            get
            {
                if (_index < _realizedChildren.Count)
                    return _realizedChildren[_index].Value;

                return null;
            }
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_index < _realizedChildren.Count)
            {
                _index++;
                return _index < _realizedChildren.Count;
            }

            return false;
        }

        public void Reset()
        {
        }

        private readonly List<KeyValuePair<int, Control>> _realizedChildren;
        private int _index;
    }
}