using Avalonia.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;

namespace FluentAvalonia.UI.Controls;

/// <summary>
///     Defines a navigation host capable of loading pages, maintaining back/forward
///     navigation stacks, and raising navigation lifecycle events.
/// </summary>
/// <remarks>
///     Pages are created exclusively through <see cref="NavigationPageFactory" />;
///     reflection-based page creation is not supported.
/// </remarks>
public interface IFAFrame
{
    //////////////////////////////////////////
    //////// NAVIGATION STATE ////////////////
    //////////////////////////////////////////

    /// <summary>
    ///     Gets the collection of entries in the back navigation history.
    /// </summary>
    IList<FAPageStackEntry> BackStack { get; }

    /// <summary>
    ///     Gets the collection of entries in the forward navigation history.
    /// </summary>
    IList<FAPageStackEntry> ForwardStack { get; }

    /// <summary>
    ///     Gets a value indicating whether there is at least one entry in the back navigation history.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    ///     Gets a value indicating whether there is at least one entry in the forward navigation history.
    /// </summary>
    bool CanGoForward { get; }

    /// <summary>
    ///     Gets the number of entries in the back navigation history.
    /// </summary>
    int BackStackDepth { get; }

    /// <summary>
    ///     Gets the entry representing the currently displayed page.
    /// </summary>
    FAPageStackEntry CurrentEntry { get; }

    /// <summary>
    ///     Gets the type of the currently displayed page.
    /// </summary>
    Type SourcePageType { get; }

    /// <summary>
    ///     Gets or sets the maximum number of pages retained in the page cache.
    ///     <para>Set to 0 to disable caching.</para>
    /// </summary>
    int CacheSize { get; set; }

    /// <summary>
    ///     Gets or sets whether navigation history tracking is enabled.
    /// </summary>
    bool IsNavigationStackEnabled { get; set; }

    /// <summary>
    ///     Gets or sets the factory used to create page instances.
    ///     Must be set before navigating.
    /// </summary>
    IFANavigationPageFactory NavigationPageFactory { get; set; }

    //////////////////////////////////////////
    //////// NAVIGATION METHODS //////////////
    //////////////////////////////////////////

    /// <summary>
    ///     Navigates to the most recent entry in the back navigation history.
    /// </summary>
    /// <remarks>
    ///     Requires <see cref="CacheSize" /> &gt; 0, otherwise an <see cref="InvalidOperationException" /> is thrown.
    /// </remarks>
    void GoBack();

    /// <summary>
    ///     Navigates to the most recent entry in the back navigation history,
    ///     using the specified transition animation.
    /// </summary>
    /// <param name="infoOverride">Info about the animated transition to use.</param>
    /// <remarks>
    ///     Requires <see cref="CacheSize" /> &gt; 0, otherwise an <see cref="InvalidOperationException" /> is thrown.
    /// </remarks>
    void GoBack(FANavigationTransitionInfo infoOverride);

    /// <summary>
    ///     Navigates to the most recent entry in the forward navigation history.
    /// </summary>
    /// <remarks>
    ///     Requires <see cref="CacheSize" /> &gt; 0, otherwise an <see cref="InvalidOperationException" /> is thrown.
    /// </remarks>
    void GoForward();

    /// <summary>Navigates to the specified page type.</summary>
    bool Navigate(Type sourcePageType);

    /// <summary>Navigates to the specified page type, passing a navigation parameter.</summary>
    bool Navigate(Type sourcePageType, object parameter);

    /// <summary>Navigates to the specified page type with a transition override.</summary>
    bool Navigate(Type sourcePageType, object parameter, FANavigationTransitionInfo infoOverride);

    /// <summary>Navigates to the specified page type with navigation options.</summary>
    bool NavigateToType(Type sourcePageType, object parameter, FAFrameNavigationOptions navOptions);

    //////////////////////////////////////////
    //////// EVENTS //////////////////////////
    //////////////////////////////////////////

    /// <summary>
    ///     Occurs when a navigation request is about to begin.
    ///     Set <see cref="FANavigatingCancelEventArgs.Cancel" /> to <c>true</c> to cancel the navigation.
    /// </summary>
    event EventHandler<FANavigatingCancelEventArgs> Navigating;

    /// <summary>
    ///     Occurs when a navigation has completed.
    /// </summary>
    event EventHandler<FANavigationEventArgs> Navigated;

    /// <summary>
    ///     Occurs when a navigation attempt fails.
    /// </summary>
    event EventHandler<FANavigationFailedEventArgs> NavigationFailed;

    /// <summary>
    ///     Occurs when a navigation is cancelled by the Navigating event or by a page's
    ///     NavigatingFrom handler.
    /// </summary>
    event EventHandler<FANavigationEventArgs> NavigationStopped;
}