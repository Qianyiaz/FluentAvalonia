using FluentAvalonia.UI.Media.Animation;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Provides data for the NavigationView.SelectionChanged event.
/// </summary>
public class FANavigationViewSelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the newly selected menu item.
    /// </summary>
    public object SelectedItem { get; internal set; }

    /// <summary>
    /// Gets the container for the selected item.
    /// </summary>
    public FANavigationViewItemBase SelectedItemContainer { get; internal set; }

    /// <summary>
    /// Gets the navigation transition recommended for the direction of the navigation.
    /// </summary>
    public FANavigationTransitionInfo RecommendedNavigationTransitionInfo { get; internal set; }
}
