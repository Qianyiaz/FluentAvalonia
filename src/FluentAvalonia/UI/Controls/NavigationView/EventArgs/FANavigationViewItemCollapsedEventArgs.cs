namespace FluentAvalonia.UI.Controls;

/// <summary>
///     Provides event data for the NavigationViewItem.ItemCollapsed event.
/// </summary>
public class FANavigationViewItemCollapsedEventArgs : EventArgs
{
    private readonly FANavigationView _navigationView;

    public FANavigationViewItemCollapsedEventArgs(FANavigationView navigationView)
    {
        _navigationView = navigationView;
    }

    /// <summary>
    ///     Gets the object that has been collapsed after the NavigationViewItem.ItemCollapsed event.
    /// </summary>
    public object CollapsedItem
    {
        get
        {
            if (field != null) return field;
            if (_navigationView != null)
            {
                field = _navigationView.MenuItemFromContainer(CollapsedItemContainer);
                return field;
            }

            return null;
        }
    }

    /// <summary>
    ///     Gets the container of the object that was collapsed in the NavigationViewItem.ItemCollapsed event.
    /// </summary>
    public FANavigationViewItemBase CollapsedItemContainer { get; internal set; }
}