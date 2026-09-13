using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;

namespace FluentAvalonia.UI.Controls;

/// <summary>
///     Displays <see cref="UserControl" /> instances (Pages in WinUI), supports navigation to new pages,
///     and maintains a navigation history to support forward and backward navigation.
/// </summary>
/// <remarks>
///     This frame requires <see cref="NavigationPageFactory" /> to be set before navigating.
///     Pages are created exclusively by <see cref="IFANavigationPageFactory" />; reflection-based
///     page creation is not supported.
///     <para>
///         (<see cref="CacheSize" /> &gt; 0); otherwise they throw <see cref="InvalidOperationException" />.
///     </para>
/// </remarks>
[TemplatePart(s_tpContentPresenter, typeof(ContentPresenter))]
public partial class FAFrame : ContentControl, IFAFrame
{
    private const string s_tpContentPresenter = "ContentPresenter";

    // Key: page Type. _cacheOrder maintains FIFO eviction order.
    private readonly Dictionary<Type, Control> _cache = new(10);
    private readonly Queue<Type> _cacheOrder = new(10);

    private CancellationTokenSource _cts;
    private ContentPresenter _presenter;

    public FAFrame()
    {
        var back = new AvaloniaList<FAPageStackEntry>();
        var forw = new AvaloniaList<FAPageStackEntry>();

        back.CollectionChanged += OnBackStackChanged;
        forw.CollectionChanged += OnForwardStackChanged;

        BackStack = back;
        ForwardStack = forw;
    }

    /// <summary>
    ///     Navigates to the most recent item in back navigation history, if a Frame manages its own navigation history.
    /// </summary>
    /// <remarks>
    ///     Requires <see cref="CacheSize" /> &gt; 0, otherwise an <see cref="InvalidOperationException" /> is thrown.
    /// </remarks>
    public void GoBack() => GoBack(null);

    /// <summary>
    ///     Navigates to the most recent item in back navigation history, if a Frame manages its own navigation history,
    ///     and specifies the animated transition to use.
    /// </summary>
    /// <param name="infoOverride">Info about the animated transition to use.</param>
    /// <remarks>
    ///     Requires <see cref="CacheSize" /> &gt; 0, otherwise an <see cref="InvalidOperationException" /> is thrown.
    /// </remarks>
    public void GoBack(FANavigationTransitionInfo infoOverride)
    {
        if (!CanGoBack)
            return;

        var entry = _backStack[^1];
        entry.NavigationTransitionInfo = infoOverride ?? CurrentEntry?.NavigationTransitionInfo;

        NavigateCore(entry, FANavigationMode.Back);
    }

    /// <summary>
    ///     Navigates to the most recent item in forward navigation history, if a Frame manages its own navigation history.
    /// </summary>
    /// <remarks>
    ///     Requires <see cref="CacheSize" /> &gt; 0, otherwise an <see cref="InvalidOperationException" /> is thrown.
    /// </remarks>
    public void GoForward()
    {
        if (!CanGoForward)
            return;

        NavigateCore(_forwardStack[^1], FANavigationMode.Forward);
    }

    public bool Navigate(Type sourcePageType)
        => Navigate(sourcePageType, null, null);

    public bool Navigate(Type sourcePageType, object parameter)
        => Navigate(sourcePageType, parameter, null);

    public bool Navigate(Type sourcePageType, object parameter, FANavigationTransitionInfo infoOverride)
    {
        ArgumentNullException.ThrowIfNull(sourcePageType);

        try
        {
            var page = GetOrCreatePage(sourcePageType);
            var entry = new FAPageStackEntry(sourcePageType, parameter, infoOverride)
            {
                Instance = page
            };

            return NavigateCore(entry, FANavigationMode.New);
        }
        catch (Exception ex)
        {
            NavigationFailed?.Invoke(this, new FANavigationFailedEventArgs(ex, sourcePageType));
            return false;
        }
    }

    public bool NavigateToType(Type sourcePageType, object parameter, FAFrameNavigationOptions navOptions)
    {
        ArgumentNullException.ThrowIfNull(sourcePageType);

        try
        {
            var page = GetOrCreatePage(sourcePageType);
            var entry = new FAPageStackEntry(sourcePageType, parameter, navOptions?.TransitionInfoOverride)
            {
                Instance = page
            };

            return NavigateCore(entry, FANavigationMode.New, navOptions);
        }
        catch (Exception ex)
        {
            NavigationFailed?.Invoke(this, new FANavigationFailedEventArgs(ex, sourcePageType));
            return false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            if (change.NewValue == null) CurrentEntry = null;
        }
        else if (change.Property == IsNavigationStackEnabledProperty)
        {
            if (!change.GetNewValue<bool>())
            {
                _backStack.Clear();
                _forwardStack.Clear();
                ClearCache();
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presenter = e.NameScope.Find<ContentPresenter>(s_tpContentPresenter);
    }

    protected override bool RegisterContentPresenter(ContentPresenter presenter)
    {
        if (presenter.Name == "ContentPresenter")
            return true;

        return base.RegisterContentPresenter(presenter);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (TopLevel.GetTopLevel(this) is { } tl) tl.BackRequested += OnTopLevelBackRequested;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (TopLevel.GetTopLevel(this) is { } tl) tl.BackRequested -= OnTopLevelBackRequested;
    }

    /// <summary>
    ///     dispatches through <see cref="IFANavigationPageFactory.GetPage(Type)" />.
    /// </summary>
    private Control GetOrCreatePage(Type pageType)
    {
        if (CacheSize > 0 && _cache.TryGetValue(pageType, out var cached))
            return cached;

        if (NavigationPageFactory is null)
            throw new InvalidOperationException(
                "FAFrame requires NavigationPageFactory to be set before navigating. " +
                "Assign an IFANavigationPageFactory instance to the Frame's NavigationPageFactory property.");

        var page = NavigationPageFactory.GetPage(pageType);

        if (CacheSize > 0)
            CachePage(pageType, page);

        return page;
    }

    private bool NavigateCore(FAPageStackEntry entry, FANavigationMode mode, FAFrameNavigationOptions options = null)
    {
        try
        {
            var ea = new FANavigatingCancelEventArgs(mode,
                entry.NavigationTransitionInfo,
                entry.Parameter,
                entry.SourcePageType);

            Navigating?.Invoke(this, ea);

            if (ea.Cancel)
            {
                OnNavigationStopped(entry, mode);
                return false;
            }

            // Ask the current page if it can navigate away
            if (CurrentEntry?.Instance is { } oldPage)
            {
                ea.RoutedEvent = NavigatingFromEvent;
                oldPage.RaiseEvent(ea);

                if (ea.Cancel)
                {
                    OnNavigationStopped(entry, mode);
                    return false;
                }
            }

            // entry.Instance is set by the caller for Navigate<T>/NavigateToType<T>.
            // For GoBack/GoForward, entry.Instance is null - resolve from cache if
            // available, otherwise recreate via NavigationPageFactory.
            // With CacheSize == 0, GoBack/GoForward produces a *new* page instance;
            // previous page state will NOT be restored.
            entry.Instance ??= GetOrCreatePage(entry.SourcePageType);

            var oldEntry = CurrentEntry;
            CurrentEntry = entry;

            var navEa = new FANavigationEventArgs(
                entry.Instance,
                mode, entry.NavigationTransitionInfo,
                entry.Parameter,
                entry.SourcePageType);

            // Old page is now unloaded, raise OnNavigatedFrom
            if (oldEntry?.Instance is { } leaving)
            {
                navEa.RoutedEvent = NavigatedFromEvent;
                leaving.RaiseEvent(navEa);

                // Release reference so GC can collect it (unless caching retains it)
                oldEntry.Instance = null;
            }

            SetContentAndAnimate(entry);

            var addToNavStack = options?.IsNavigationStackEnabled ?? IsNavigationStackEnabled;

            if (addToNavStack)
                switch (mode)
                {
                    case FANavigationMode.New:
                        ForwardStack.Clear();
                        if (oldEntry != null) BackStack.Add(oldEntry);
                        break;

                    case FANavigationMode.Back:
                        ForwardStack.Add(oldEntry);
                        BackStack.Remove(entry);
                        break;

                    case FANavigationMode.Forward:
                        BackStack.Add(oldEntry);
                        ForwardStack.Remove(entry);
                        break;

                    case FANavigationMode.Refresh:
                        break;
                }

            SourcePageType = entry.SourcePageType;

            Navigated?.Invoke(this, navEa);

            Dispatcher.UIThread.Post(() =>
            {
                if (entry.Instance is { } newPage)
                {
                    navEa.RoutedEvent = NavigatedToEvent;
                    newPage.RaiseEvent(navEa);
                }
            }, DispatcherPriority.Render);

            return true;
        }
        catch (Exception ex)
        {
            NavigationFailed?.Invoke(this, new FANavigationFailedEventArgs(ex, entry.SourcePageType));
            return false;
        }
    }

    private void OnNavigationStopped(FAPageStackEntry entry, FANavigationMode mode)
    {
        NavigationStopped?.Invoke(this, new FANavigationEventArgs(entry.Instance,
            mode, entry.NavigationTransitionInfo, entry.Parameter, entry.SourcePageType));
    }

    private void OnForwardStackChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        var oldCount = _forwardStack.Count - (e.NewItems?.Count ?? 0) + (e.OldItems?.Count ?? 0);

        var oldForward = oldCount > 0;
        var newForward = _forwardStack.Count > 0;
        RaisePropertyChanged(CanGoForwardProperty, oldForward, newForward);
    }

    private void OnBackStackChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        var oldCount = _backStack.Count - (e.NewItems?.Count ?? 0) + (e.OldItems?.Count ?? 0);

        var oldBack = oldCount > 0;
        var newBack = _backStack.Count > 0;
        RaisePropertyChanged(CanGoBackProperty, oldBack, newBack);
        RaisePropertyChanged(BackStackDepthProperty, oldCount, _backStack.Count);
    }

    private void CachePage(Type key, Control page)
    {
        _cache[key] = page;
        _cacheOrder.Enqueue(key);

        // FIFO eviction - matches original RemoveAt(0) behavior
        while (_cache.Count > CacheSize && _cacheOrder.Count > 0)
        {
            var oldest = _cacheOrder.Dequeue();
            _cache.Remove(oldest);
        }
    }

    private void ClearCache()
    {
        _cache.Clear();
        _cacheOrder.Clear();
    }

    private void SetContentAndAnimate(FAPageStackEntry entry)
    {
        if (entry == null)
            return;

        Content = entry.Instance;

        if (_presenter != null)
        {
            //Default to entrance transition
            entry.NavigationTransitionInfo ??= new FAEntranceNavigationTransitionInfo();
            _presenter.Opacity = 0;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Post the animation otherwise pages that take slightly longer to load won't
            // have an animation since it will run before layout is complete
            Dispatcher.UIThread.Post(() => { entry.NavigationTransitionInfo.RunAnimation(_presenter, _cts.Token); },
                DispatcherPriority.Render);
        }
    }

    private void OnTopLevelBackRequested(object sender, RoutedEventArgs e)
    {
        if (!e.Handled && IsNavigationStackEnabled && CanGoBack)
        {
            GoBack();
            e.Handled = true;
        }
    }
}