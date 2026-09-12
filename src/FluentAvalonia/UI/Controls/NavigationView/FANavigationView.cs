using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using FluentAvalonia.Core;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.VisualTree;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Represents a container that enables navigation of app content. It has a header, 
/// a view for the main content, and a menu pane for navigation commands.
/// </summary>
public partial class FANavigationView : HeaderedContentControl
{
    public FANavigationView()
    {
        TemplateSettings = new FANavigationViewTemplateSettings();
        _sizeChangedRevoker = this.GetObservable(BoundsProperty).Subscribe(OnSizeChanged);
        _selectionModelSource = new AvaloniaList<IEnumerable>(2) { null, null };
        _topDataProvider = new TopNavigationViewDataProvider(this);

        MenuItems = new AvaloniaList<object>();
        FooterMenuItems = new AvaloniaList<object>();

        _topDataProvider.OnRawDataChanged((args) => OnTopNavDataSourceChanged(args));

        Loaded += OnNavViewLoaded;

        _selectionModel = new SelectionModel { SingleSelect = true, Source = _selectionModelSource };
        _selectionModel.SelectionChanged += OnSelectionModelSelectionChanged;
        _selectionModel.ChildrenRequested += OnSelectionModelChildrenRequested;

        _itemsFactory = new NavigationViewItemsFactory();
    }


    ///////////////////////////////////////
    //////// OVERRIDE METHODS ////////////
    /////////////////////////////////////

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        try
        {
            _fromOnApplyTemplate = true;
            UnhookEventsAndClearFields();
            base.OnApplyTemplate(e);

            _paneToggleButton = e.NameScope.Get<Button>(s_tpTogglePaneButton);
            if (_paneToggleButton != null)
            {
                _paneToggleButton.Click += OnPaneToggleButtonClick;
                SetPaneToggleButtonAutomationName();
            }

            _leftNavPaneHeaderContentBorder = e.NameScope.Get<ContentControl>(s_tpPaneHeaderContentBorder);
            _leftNavPaneCustomContentBorder = e.NameScope.Get<ContentControl>(s_tpPaneCustomContentBorder);
            _leftNavFooterContentBorder = e.NameScope.Get<ContentControl>(s_tpFooterContentBorder);
            _paneHeaderOnTopPane = e.NameScope.Get<ContentControl>(s_tpPaneHeaderOnTopPane);
            _paneTitleOnTopPane = e.NameScope.Get<ContentControl>(s_tpPaneTitleOnTopPane);
            _paneCustomContentOnTopPane = e.NameScope.Get<ContentControl>(s_tpPaneCustomContentOnTopPane);
            _paneFooterOnTopPane = e.NameScope.Get<ContentControl>(s_tpPaneFooterOnTopPane);

            _splitView = e.NameScope.Get<SplitView>(s_tpRootSplitView);
            if (_splitView != null)
            {
                _splitViewRevokers = new FACompositeDisposable(
                    _splitView.GetPropertyChangedObservable(SplitView.IsPaneOpenProperty).Subscribe(OnSplitViewClosedCompactChanged),
                    _splitView.GetPropertyChangedObservable(SplitView.DisplayModeProperty).Subscribe(OnSplitViewClosedCompactChanged));

                _splitView.PaneClosed += OnSplitViewPaneClosed;
                _splitView.PaneClosing += OnSplitViewPaneClosing;
                _splitView.PaneOpened += OnSplitViewPaneOpened;
                _splitView.PaneOpening += OnSplitViewPaneOpening;

                UpdateIsClosedCompact();
            }

            _topNavGrid = e.NameScope.Get<Grid>(s_tpTopNavGrid);

            _leftNavRepeater = ConfigureRepeater(
                e.NameScope.Get<FAItemsRepeater>(s_tpMenuItemsHost), withFocusEvents: true);

            _topNavRepeater = ConfigureRepeater(
                e.NameScope.Get<FAItemsRepeater>(s_tpTopNavMenuItemsHost), withFocusEvents: true);

            _topNavRepeaterOverflowView = ConfigureRepeater(
                e.NameScope.Get<FAItemsRepeater>(s_tpTopNavMenuItemsOverflowHost), withFocusEvents: false);

            _topNavOverflowButton = e.NameScope.Get<Button>(s_tpTopNavOverflowButton);
            if (_topNavOverflowButton != null)
            {
                (_topNavOverflowButton.Flyout as PopupFlyoutBase)?.Closing += OnFlyoutClosing;
                // AutomationProperties.Name and ToolTip.Tip are set directly in XAML.
            }

            _leftNavFooterMenuRepeater = ConfigureRepeater(
                e.NameScope.Get<FAItemsRepeater>(s_tpFooterMenuItemsHost), withFocusEvents: true);

            _topNavFooterMenuRepeater = ConfigureRepeater(
                e.NameScope.Get<FAItemsRepeater>(s_tpTopFooterMenuItemsHost), withFocusEvents: true);

            _topNavContentOverlayAreaGrid = e.NameScope.Get<Border>(s_tpTopNavContentOverlayAreaGrid);
            _leftNavAutoSuggestBoxPresenter = e.NameScope.Get<ContentControl>(s_tpPaneAutoSuggestBoxPresenter);
            _topNavAutoSuggestBoxPresenter = e.NameScope.Get<ContentControl>(s_tpTopPaneAutoSuggestBoxPresenter);

            _paneContentGrid = e.NameScope.Get<Grid>(s_tpPaneContentGrid);
            _contentLeftPadding = e.NameScope.Get<Rectangle>(s_tpContentLeftPadding);

            var placeholderGrid = e.NameScope.Get<Grid>(s_tpPlaceholderGrid);
            if (placeholderGrid != null)
            {
                _paneHeaderCloseButtonColumn = placeholderGrid.ColumnDefinitions[0];
                _paneHeaderToggleButtonColumn = placeholderGrid.ColumnDefinitions[1];
                _paneHeaderContentBorderRow = placeholderGrid.RowDefinitions[0];
            }

            _paneTitleFrameworkElement = e.NameScope.Get<Control>(s_tpPaneTitleTextBlock);
            _paneTitlePresenter = e.NameScope.Get<ContentControl>(s_tpPaneTitlePresenter);

            _paneTitleHolderFrameworkElement = e.NameScope.Get<Control>(s_tpPaneTitleHolder);
            if (_paneTitleHolderFrameworkElement != null)
            {
                _paneTitleHolderRevoker = _paneTitleHolderFrameworkElement
                    .GetObservable(BoundsProperty).Subscribe(OnPaneTitleHolderSizeChanged);
            }

            _paneSearchButton = e.NameScope.Get<Button>(s_tpPaneAutoSuggestButton);
            if (_paneSearchButton != null)
                _paneSearchButton.Click += OnPaneSearchButtonClick;

            _backButton = e.NameScope.Get<Button>(s_tpNavigationViewBackButton);
            if (_backButton != null)
                _backButton.Click += OnBackButtonClicked;

            _closeButton = e.NameScope.Get<Button>(s_tpNavigationViewCloseButton);
            if (_closeButton != null)
                _closeButton.Click += OnPaneToggleButtonClick;

            if (_paneContentGrid != null)
                _itemsContainerRow = _paneContentGrid.RowDefinitions[_paneContentGrid.RowDefinitions.Count - 1];

            _menuItemsScrollViewer = e.NameScope.Get<ScrollViewer>(s_tpMenuItemsScrollViewer);
            _footerItemsScrollViewer = e.NameScope.Get<ScrollViewer>(s_tpFooterItemsScrollViewer);

            _itemsContainer = e.NameScope.Find<Control>(s_tpItemsContainerGrid);
            if (_itemsContainerRow != null)
                _itemsContainerSizeRevoker = _itemsContainer.GetObservable(BoundsProperty).Subscribe(OnItemsContainerSizeChanged);

            UpdatePaneShadow();
            _appliedTemplate = true;

            UpdatePaneDisplayMode();
            UpdateHeaderVisibility();
            UpdatePaneTitleFrameworkElementParents();
            UpdateTitleBarPadding();
            UpdatePaneTabFocusNavigation();
            UpdateBackAndCloseButtonsVisibility();
            UpdatePaneVisibility();
            UpdateVisualState();
            UpdatePaneLayout();
            UpdatePaneOverlayGroup();

            UpdateRepeaterItemsSource(true);
            UpdateFooterRepeaterItemsSource(true, true);
        }
        finally
        {
            _fromOnApplyTemplate = false;
        }
    }

    private FAItemsRepeater ConfigureRepeater(FAItemsRepeater repeater, bool withFocusEvents)
    {
        if (repeater == null)
            return null;

        (repeater.Layout as FAStackLayout).DisableVirtualization = true;
        repeater.ElementPrepared += OnRepeaterElementPrepared;
        repeater.ElementClearing += OnRepeaterElementClearing;
        repeater.ItemTemplate = _itemsFactory;

        if (withFocusEvents)
        {
            repeater.Loaded += OnRepeaterLoaded;
            repeater.GettingFocus += OnRepeaterGettingFocus;
        }

        return repeater;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTopNavigationView && IsTopPrimaryListVisible)
        {
            if (double.IsInfinity(availableSize.Width))
            {
                _topDataProvider.MoveAllItemsToPrimaryList();
            }
            else
            {
                HandleTopNavigationMeasureOverride(availableSize);
#if DEBUG
                if (_topDataProvider.Size > 0)
                    Debug.Assert(_topDataProvider.GetPrimaryItems().Count > 0);
#endif
            }
        }

        this.LayoutUpdated += OnLayoutUpdated;
        return base.MeasureOverride(availableSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CompactModeThresholdWidthProperty ||
            change.Property == ExpandedModeThresholdWidthProperty)
        {
            UpdateAdaptiveLayout(Bounds.Width);
        }
        else if (change.Property == AlwaysShowHeaderProperty || change.Property == HeaderProperty)
        {
            UpdateHeaderVisibility();
        }
        else if (change.Property == PaneTitleProperty)
        {
            UpdatePaneTitleFrameworkElementParents();
            UpdateBackAndCloseButtonsVisibility();
            UpdatePaneToggleSize();
        }
        else if (change.Property == PaneDisplayModeProperty)
        {
            _wasForceClosed = false;
            var (oldValue, newValue) = change.GetOldAndNewValue<FANavigationViewPaneDisplayMode>();
            UpdatePaneToggleButtonVisibility();
            UpdatePaneDisplayMode(oldValue, newValue);
            UpdatePaneTitleFrameworkElementParents();
            UpdatePaneVisibility();
            UpdateVisualState();
            UpdatePaneButtonWidths();
        }
        else if (change.Property == IsPaneVisibleProperty)
        {
            UpdatePaneVisibility();
            UpdateVisualStateForDisplayModeGroup(DisplayMode);

            if (!IsPaneVisible && IsPaneOpen)
                ClosePane();
            else if (IsPaneVisible && DisplayMode == FANavigationViewDisplayMode.Expanded && !IsPaneOpen)
                OpenPane();
        }
        else if (change.Property == AutoCompleteBoxProperty)
        {
            InvalidateTopNavPrimaryLayout();
            UpdateVisualState();
        }
        else if (change.Property == IsPaneToggleButtonVisibleProperty)
        {
            UpdatePaneTitleFrameworkElementParents();
            UpdateBackAndCloseButtonsVisibility();
            UpdatePaneToggleButtonVisibility();
            UpdateVisualState();
        }
        else if (change.Property == CompactPaneLengthProperty)
        {
            UpdatePaneButtonWidths();
        }
        else if (change.Property == MenuItemTemplateProperty ||
            change.Property == MenuItemTemplateSelectorProperty)
        {
            UpdateNavigationViewItemsFactory();
        }
        else if (change.Property == PaneFooterProperty)
        {
            UpdatePaneLayout();
        }
        else if (change.Property == SelectedItemProperty)
        {
            OnSelectedItemPropertyChanged(change.OldValue, change.NewValue);
        }
        else if (change.Property == IsBackButtonVisibleProperty)
        {
            UpdateBackAndCloseButtonsVisibility();
            UpdateAdaptiveLayout(Bounds.Width);
            if (IsTopNavigationView)
                InvalidateTopNavPrimaryLayout();
            _backButton?.InvalidateMeasure();
            UpdatePaneLayout();
        }
        else if (change.Property == MenuItemsSourceProperty || change.Property == MenuItemsProperty)
        {
            UpdateRepeaterItemsSource(true);
        }
        else if (change.Property == FooterMenuItemsSourceProperty || change.Property == FooterMenuItemsProperty)
        {
            UpdateFooterRepeaterItemsSource(true, true);
        }
        else if (change.Property == IsPaneOpenProperty)
        {
            OnIsPaneOpenChanged();
            UpdateVisualStateForDisplayModeGroup(_displayMode);
        }
        else if (change.Property == OpenPaneLengthProperty)
        {
            UpdateOpenPaneWidth(Bounds.Width);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _tabKeyPrecedesFocusChange = false;
        switch (e.Key)
        {
            case Key.Back:
                if (IsPaneOpen && IsLightDismissable)
                    e.Handled = AttemptClosePaneLightly();
                break;

            case Key.Tab:
                _tabKeyPrecedesFocusChange = true;
                break;

            case Key.Left:
                if ((e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt && IsPaneOpen && IsLightDismissable)
                    e.Handled = AttemptClosePaneLightly();
                break;
        }

        base.OnKeyDown(e);
    }

    protected override bool RegisterContentPresenter(ContentPresenter presenter)
        => presenter.Name == "ContentPresenter" || base.RegisterContentPresenter(presenter);

    protected override AutomationPeer OnCreateAutomationPeer()
        => new FANavigationViewAutomationPeer(this);

    private void OnLayoutUpdated(object sender, EventArgs e)
    {
        this.LayoutUpdated -= OnLayoutUpdated;

        if (_lastSelectedItemPendingAnimationInTopNav != null)
        {
            var lastItem = _lastSelectedItemPendingAnimationInTopNav;
            _lastSelectedItemPendingAnimationInTopNav = null;
            AnimateSelectionChanged(lastItem);
        }

        if (_orientationChangedPendingAnimation)
        {
            _orientationChangedPendingAnimation = false;
            AnimateSelectionChanged(SelectedItem);
        }
    }

    private void OnNavViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_updateVisualStateForDisplayModeFromOnLoaded)
        {
            _updateVisualStateForDisplayModeFromOnLoaded = false;
            UpdateVisualStateForDisplayModeGroup(DisplayMode);
        }

        UpdatePaneButtonWidths();
    }


    /////////////////////////////////////////////
    //////// ITEMS REPEATER RELATED ////////////
    ///////////////////////////////////////////

    private void OnRepeaterLoaded(object sender, RoutedEventArgs args)
    {
        var item = SelectedItem;
        if (item != null && !IsSelectionSuppressed(item))
        {
            var nvi = NavigationViewItemOrSettingsContentFromData(item);
            nvi.IsSelected = true;
            UpdateSelectionModelSelectionForSelectedItem(item);
            AnimateSelectionChanged(item);
        }
    }

    private void UpdateRepeaterItemsSource(bool forceSelectionModelUpdate)
    {
        IEnumerable itemsSource;
        if (MenuItemsSource != null)
        {
            itemsSource = MenuItemsSource;
        }
        else
        {
            UpdateSelectionForMenuItems();
            itemsSource = _menuItems;
        }

        if (forceSelectionModelUpdate)
            _selectionModelSource[0] = itemsSource;

        _menuItemsSource?.CollectionChanged -= OnMenuItemsSourceCollectionChanged;

        if (itemsSource != null)
        {
            _menuItemsSource = ItemsSourceView.GetOrCreate(itemsSource);
            _menuItemsSource.CollectionChanged += OnMenuItemsSourceCollectionChanged;
        }

        if (IsTopNavigationView)
        {
            UpdateLeftRepeaterItemSource(null);
            UpdateTopNavRepeatersItemSource(itemsSource);
            InvalidateTopNavPrimaryLayout();
        }
        else
        {
            UpdateTopNavRepeatersItemSource(null);
            UpdateLeftRepeaterItemSource(itemsSource);
        }
    }

    private void UpdateLeftRepeaterItemSource(IEnumerable items)
    {
        UpdateItemsRepeaterItemsSource(_leftNavRepeater, items);
        UpdatePaneLayout();
    }

    private void UpdateTopNavRepeatersItemSource(IEnumerable items)
    {
        _topDataProvider.SetDataSource(items);
        UpdateTopNavPrimaryRepeaterItemsSource(items);
        UpdateTopNavOverflowRepeaterItemsSource(items);
    }

    private void UpdateTopNavPrimaryRepeaterItemsSource(IEnumerable items)
        => UpdateItemsRepeaterItemsSource(_topNavRepeater,
            items != null ? _topDataProvider.GetPrimaryItems() : null);

    private void UpdateTopNavOverflowRepeaterItemsSource(IEnumerable items)
    {
        if (_topNavRepeaterOverflowView == null)
            return;

        if (_topNavRepeaterOverflowView.ItemsSourceView != null)
            _topNavRepeaterOverflowView.ItemsSourceView.CollectionChanged -= OnOverflowItemsSourceCollectionChanged;

        if (items != null)
        {
            _topNavRepeaterOverflowView.ItemsSource = _topDataProvider.GetOverflowItems();
            if (_topNavRepeater.ItemsSourceView != null)
                _topNavRepeaterOverflowView.ItemsSourceView.CollectionChanged += OnOverflowItemsSourceCollectionChanged;
        }
        else
        {
            _topNavRepeaterOverflowView.ItemsSource = null;
        }
    }

    private static void UpdateItemsRepeaterItemsSource(FAItemsRepeater ir, IEnumerable source)
    {
        if (ir != null)
            ir.ItemsSource = source;
    }

    private void UpdateFooterRepeaterItemsSource(bool sourceCollectionReset, bool sourceCollectionChanged)
    {
        if (!_appliedTemplate)
            return;

        IEnumerable itemsSource;
        if (FooterMenuItemsSource != null)
        {
            itemsSource = FooterMenuItemsSource;
        }
        else
        {
            UpdateSelectionForMenuItems();
            itemsSource = _footerMenuItems;
        }

        UpdateItemsRepeaterItemsSource(_leftNavFooterMenuRepeater, null);
        UpdateItemsRepeaterItemsSource(_topNavFooterMenuRepeater, null);

        if (sourceCollectionChanged || sourceCollectionReset)
        {
            if (sourceCollectionReset && _footerItemsSource != null)
            {
                _footerItemsSource.CollectionChanged -= OnFooterItemsSourceCollectionChanged;
                _footerItemsSource = null;
            }

            _footerItemsSource ??= ItemsSourceView.GetOrCreate(itemsSource);
            _footerItemsSource.CollectionChanged += OnFooterItemsSourceCollectionChanged;

            var dataSource = new List<object>(_footerItemsSource.Count);
            for (var i = 0; i < _footerItemsSource.Count; i++)
                dataSource.Add(_footerItemsSource.GetAt(i));

            _selectionModelSource[1] = dataSource;
        }

        if (IsTopNavigationView)
        {
            UpdateItemsRepeaterItemsSource(_topNavFooterMenuRepeater, _selectionModelSource[1] as IEnumerable);
        }
        else if (_leftNavFooterMenuRepeater != null)
        {
            UpdateItemsRepeaterItemsSource(_leftNavFooterMenuRepeater, _selectionModelSource[1] as IEnumerable);
            _leftNavFooterMenuRepeater.InvalidateMeasure();
            _leftNavFooterMenuRepeater.InvalidateArrange();
            UpdatePaneLayout();
        }
    }

    internal void OnRepeaterElementPrepared(object sender, FAItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FANavigationViewItemBase nvib)
            return;

        nvib.SetNavigationViewParent(this);
        nvib.IsTopLevelItem = IsTopLevelItem(nvib);
        nvib.IsInNavigationViewOwnedRepeater = true;

        var ir = sender as FAItemsRepeater;
        NavigationViewRepeaterPosition GetPosition()
        {
            if (IsTopNavigationView)
            {
                if (ir == _topNavRepeater) return NavigationViewRepeaterPosition.TopPrimary;
                if (ir == _topNavFooterMenuRepeater) return NavigationViewRepeaterPosition.TopFooter;
                return NavigationViewRepeaterPosition.TopOverflow;
            }
            return ir == _leftNavFooterMenuRepeater
                ? NavigationViewRepeaterPosition.LeftFooter
                : NavigationViewRepeaterPosition.LeftNav;
        }

        nvib.Position = GetPosition();

        var parentNVI = GetParentNavigationViewItemForContainer(nvib);
        nvib.Depth = parentNVI == null ? 0 : (parentNVI.ShouldRepeaterShowInFlyout ? 0 : parentNVI.Depth + 1);

        ApplyCustomMenuItemContainerStyling(nvib);
        SetNavigationViewItemBaseRevokers(nvib);

        if (args.Element is FANavigationViewItem nvi)
        {
            var childDepth = nvib.Position == NavigationViewRepeaterPosition.TopPrimary ? 0 : nvib.Depth + 1;
            nvi.PropagateDepthToChildren(childDepth);
            SetNavigationViewItemRevokers(nvi);

            var item = MenuItemFromContainer(nvi);
            if (SelectedItem == item && nvi.IsEffectivelyVisible)
            {
                if (_isSelectionChangedPending && _pendingSelectionChangedItem != null)
                    Debug.Assert(_pendingSelectionChangedItem == item);

                nvi.LayoutUpdated += OnSelectedItemLayoutUpdated;
            }
        }
    }

    private void ApplyCustomMenuItemContainerStyling(FANavigationViewItemBase item)
    {
        item.Theme = MenuItemContainerTheme;
    }

    internal void OnRepeaterElementClearing(object sender, FAItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not FANavigationViewItemBase nvib)
            return;

        nvib.Depth = 0;
        nvib.IsTopLevelItem = false;
        nvib.IsInNavigationViewOwnedRepeater = false;
        ClearNavigationViewItemBaseRevokers(nvib);

        if (nvib is FANavigationViewItem nvi)
        {
            nvi.Tapped -= OnNavigationViewItemTapped;
            nvi.KeyDown -= OnNavigationViewItemKeyDown;
            nvi.GotFocus -= OnNavigationViewItemGotFocus;
        }
    }

    private void OnRepeaterGettingFocus(object sender, FocusChangingEventArgs e)
    {
        // Reserved for future XYKeyboardFocus support
        _tabKeyPrecedesFocusChange = false;
    }

    private void UpdateNavigationViewItemsFactory()
        => _itemsFactory.UserElementFactory(MenuItemTemplate ?? (object)MenuItemTemplateSelector);


    ////////////////////////////////////////////////
    //////// PROPERTY CHANGED HANDLERS ////////////
    //////////////////////////////////////////////

    private void OnMenuItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsTopNavigationView)
            UpdatePaneLayout();
    }

    private void OnFooterItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateFooterRepeaterItemsSource(false, true);
        UpdatePaneLayout();
    }

    private void OnOverflowItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        if (_topNavRepeaterOverflowView?.ItemsSourceView?.Count == 0)
            SetOverflowButtonVisibility(false);
    }

    private void OnSizeChanged(Rect r)
    {
        UpdateOpenPaneWidth(r.Width);
        UpdateAdaptiveLayout(r.Width);
        UpdateTitleBarPadding();
        UpdateBackAndCloseButtonsVisibility();
        UpdatePaneLayout();
    }

    private void OnItemsContainerSizeChanged(Rect rc) => UpdatePaneLayout();

    private void OnTopNavDataSourceChanged(NotifyCollectionChangedEventArgs args)
    {
        CloseTopNavigationViewFlyout();

        if (_topNavigationMode != TopNavigationViewLayoutState.Uninitialized)
            _topDataProvider.MoveAllItemsToPrimaryList();

        _lastSelectedItemPendingAnimationInTopNav = null;
    }

    private void OnSelectedItemPropertyChanged(object oldItem, object newItem)
    {
        ChangeSelection(oldItem, newItem);

        if (_appliedTemplate && IsTopNavigationView && newItem != null &&
            _topDataProvider.IndexOf(newItem) != _itemNotFound &&
            _topDataProvider.IndexOf(newItem, NavigationViewSplitVectorID.PrimaryList) == _itemNotFound)
        {
            InvalidateTopNavPrimaryLayout();
        }
    }

    private void OnIsPaneOpenChanged()
    {
        var isOpen = IsPaneOpen;
        if (isOpen && _wasForceClosed)
        {
            _wasForceClosed = false;
        }
        else if (!_isOpenPaneForInteraction && !isOpen)
        {
            _wasForceClosed = _splitView?.IsPaneOpen ?? true;
        }

        SetPaneToggleButtonAutomationName();
        UpdatePaneTabFocusNavigation();
        UpdatePaneTitleFrameworkElementParents();
        UpdatePaneOverlayGroup();
        UpdatePaneButtonWidths();
    }


    //////////////////////////////////////////////////////////
    //////// SELECTION & SELECTION MODEL RELATED ////////////
    ////////////////////////////////////////////////////////

    private void OnSelectionModelChildrenRequested(object sender, SelectionModelChildrenRequestedEventArgs e)
    {
        if (e.SourceIndex.GetSize() == 1)
        {
            e.Children = e.Source;
        }
        else if (e.Source is FANavigationViewItem nvi)
        {
            e.Children = GetChildren(nvi);
        }
        else
        {
            var children = GetChildrenForItemInIndexPath(e.SourceIndex, true);
            if (children != null)
                e.Children = children;
        }
    }

    private void OnSelectionModelSelectionChanged(object sender, SelectionModelSelectionChangedEventArgs e)
    {
        var selItem = _selectionModel.SelectedItem;

        if (_shouldIgnoreNextSelectionChange || selItem == SelectedItem || !_appliedTemplate)
            return;

        var setSelectedItem = true;
        var selIndex = _selectionModel.SelectedIndex;

        if (IsTopNavigationView && selIndex != IndexPath.Unselected && selIndex.GetSize() > 1 &&
            selIndex.GetAt(0) == _mainMenuBlockIndex && !_topDataProvider.IsItemInPrimaryList(selIndex.GetAt(1)))
        {
            // Item is in overflow
            var selContainer = GetContainerForIndexPath(selIndex);
            var hasChildren = selContainer is FANavigationViewItem nvi && DoesNavigationViewItemHaveChildren(nvi);

            if (!hasChildren)
            {
                SelectAndMoveOverflowItem(selItem, selIndex, true);
                setSelectedItem = false;
            }
            else
            {
                _moveTopNavOverflowItemOnFlyoutClose = true;
            }
        }

        if (setSelectedItem)
            SetSelectedItemAndExpectItemInvokeWhenSelectionChangedIfNotInvokedFromAPI(selItem);
    }

    private void SelectAndMoveOverflowItem(object selItem, IndexPath selIndex, bool closeFlyout)
    {
        try
        {
            _selectionChangeFromOverflowMenu = true;
            if (closeFlyout)
                CloseTopNavigationViewFlyout();

            if (!IsSelectionSuppressed(selItem))
                SelectOverflowItem(selItem, selIndex);
        }
        finally
        {
            _selectionChangeFromOverflowMenu = false;
        }
    }

    private void CloseFlyoutIfRequired(FANavigationViewItem selItem)
    {
        var selIndex = _selectionModel.SelectedIndex;

        var isInModeWithFlyout = false;
        if (_splitView != null)
        {
            var svdm = _splitView.DisplayMode;
            isInModeWithFlyout = (!_splitView.IsPaneOpen &&
                (svdm == SplitViewDisplayMode.CompactOverlay || svdm == SplitViewDisplayMode.CompactInline)) ||
                PaneDisplayMode == FANavigationViewPaneDisplayMode.Top;
        }

        if (isInModeWithFlyout && selIndex != IndexPath.Unselected && !DoesNavigationViewItemHaveChildren(selItem))
        {
            var rootItem = GetContainerForIndex(selIndex.GetAt(1), selIndex.GetAt(0) == _footerMenuBlockIndex);
            if (rootItem is FANavigationViewItem nvi && nvi.ShouldRepeaterShowInFlyout)
                nvi.IsExpanded = false;
        }
    }

    private void RaiseSelectionChangedEvent(object nextItem, NavigationRecommendedTransitionDirection recDir)
    {
        FANavigationViewItemBase container = null;
        if (nextItem != null)
        {
            container = NavigationViewItemBaseOrSettingsContentFromData(nextItem) as FANavigationViewItemBase
                ?? GetContainerForIndexPath(_selectionModel.SelectedIndex, false, true) as FANavigationViewItemBase;
        }

        SelectionChanged?.Invoke(this, new FANavigationViewSelectionChangedEventArgs
        {
            SelectedItem = nextItem,
            SelectedItemContainer = container,
            RecommendedNavigationTransitionInfo = CreateNavigationTransitionInfo(recDir)
        });
    }

    private void ChangeSelection(object prevItem, object nextItem)
    {
        if (IsSelectionSuppressed(nextItem))
        {
            UndoSelectionAndRevertSelectionTo(prevItem, nextItem);
            RaiseItemInvoked(nextItem);
            return;
        }

        var recDir = NavigationRecommendedTransitionDirection.Default;
        if (IsTopNavigationView)
        {
            if (_selectionChangeFromOverflowMenu)
            {
                recDir = NavigationRecommendedTransitionDirection.FromOverflow;
            }
            else if (prevItem != null && nextItem != null)
            {
                recDir = GetRecommendedTransitionDirection(
                    NavigationViewItemBaseOrSettingsContentFromData(prevItem),
                    NavigationViewItemBaseOrSettingsContentFromData(nextItem));
            }
        }

        var selItem = SelectedItem;
        if (_shouldRaiseItemInvokedAfterSelection)
        {
            _shouldRaiseItemInvokedAfterSelection = false;
            RaiseItemInvoked(nextItem, NavigationViewItemOrSettingsContentFromData(nextItem), recDir);
        }

        // Selection was modified inside ItemInvoked, skip everything here!
        if (selItem != SelectedItem)
            return;

        UnselectPrevItem(prevItem, nextItem);
        ChangeSelectStatusForItem(nextItem, true);
        UpdateSelectionModelSelectionForSelectedItem(nextItem);

        try
        {
            if (!_shouldIgnoreUIASelectionRaiseAsExpandCollapseWillRaise &&
                ControlAutomationPeer.FromElement(this) is FANavigationViewAutomationPeer p)
            {
                p.RaiseSelectionChangedEvent(prevItem, nextItem);
            }
        }
        finally
        {
            _shouldIgnoreUIASelectionRaiseAsExpandCollapseWillRaise = false;
        }

        var nvi = NavigationViewItemOrSettingsContentFromData(nextItem);
        if (nvi != null)
        {
            AnimateSelectionChanged(nextItem);
            RaiseSelectionChangedEvent(nextItem, recDir);
            ClosePaneIfNecessaryAfterItemIsClicked(nvi);
        }
        else
        {
            _isSelectionChangedPending = true;
            _pendingSelectionChangedItem = nextItem;
            _pendingSelectionChangedDirection = recDir;

            Dispatcher.UIThread.Post(CompletePendingSelectionChange);
        }
    }

    private void CompletePendingSelectionChange()
    {
        if (!_isSelectionChangedPending)
            return;

        AnimateSelectionChanged(FindLowestLevelContainerToDisplaySelectionIndicator());
        _isSelectionChangedPending = false;

        var item = _pendingSelectionChangedItem;
        var direction = _pendingSelectionChangedDirection;

        _pendingSelectionChangedItem = null;
        _pendingSelectionChangedDirection = default;

        RaiseSelectionChangedEvent(item, direction);
    }

    private void UpdateSelectionModelSelectionForSelectedItem(object selectedItem)
    {
        var base_ = NavigationViewItemBaseOrSettingsContentFromData(selectedItem);
        var indexPath = base_ is FANavigationViewItemBase c
            ? GetIndexPathForContainer(c)
            : GetIndexPathOfItem(selectedItem);

        if (indexPath == IndexPath.Unselected || indexPath.GetSize() == 0)
            return;

        try
        {
            _shouldIgnoreNextSelectionChange = true;
            UpdateSelectionModelSelection(indexPath);
        }
        finally
        {
            _shouldIgnoreNextSelectionChange = false;
        }
    }

    private void UpdateSelectionModelSelection(IndexPath ip)
    {
        var prevIP = _selectionModel.SelectedIndex;
        _selectionModel.SelectAt(ip);
        UpdateIsChildSelected(prevIP, ip);
    }

    private void UpdateIsChildSelected(IndexPath prevIP, IndexPath nextIP)
    {
        if (prevIP != IndexPath.Unselected && prevIP.GetSize() > 0)
            UpdateIsChildSelectedForIndexPath(prevIP, false);
        if (nextIP != IndexPath.Unselected && nextIP.GetSize() > 0)
            UpdateIsChildSelectedForIndexPath(nextIP, true);
    }

    private void UpdateIsChildSelectedForIndexPath(IndexPath ip, bool isChildSelected)
    {
        var cont = GetContainerForIndex(ip.GetAt(1), ip.GetAt(0) == _footerMenuBlockIndex);
        var index = 2;
        while (cont is FANavigationViewItem nvi)
        {
            nvi.IsChildSelected = isChildSelected;
            cont = null;
            if (nvi.GetRepeater is FAItemsRepeater ir && index < ip.GetSize() - 1)
            {
                cont = ir.TryGetElement(ip.GetAt(index));
                index++;
            }
        }
    }

    private void RaiseItemInvoked(object item, FANavigationViewItemBase container = null,
        NavigationRecommendedTransitionDirection recDir = NavigationRecommendedTransitionDirection.Default)
    {
        if (container == null)
        {
            container = NavigationViewItemBaseOrSettingsContentFromData(item);
            item = container?.Content;
        }
        else
        {
            item = container.Content;
        }

        ItemInvoked?.Invoke(this, new FANavigationViewItemInvokedEventArgs
        {
            InvokedItem = item,
            InvokedItemContainer = container,
            RecommendedNavigationTransitionInfo = CreateNavigationTransitionInfo(recDir)
        });
    }

    private void SetSelectedItemAndExpectItemInvokeWhenSelectionChangedIfNotInvokedFromAPI(object selItem)
        => SelectedItem = selItem;

    private void ChangeSelectStatusForItem(object item, bool selected)
    {
        var container = NavigationViewItemOrSettingsContentFromData(item);
        if (container != null)
        {
            container.IsSelected = selected;
            return;
        }

        if (!selected)
            return;

        var ip = GetIndexPathOfItem(item);
        if (ip == IndexPath.Unselected || ip.GetSize() == 0)
            return;

        try
        {
            _shouldIgnoreNextSelectionChange = true;
            UpdateSelectionModelSelection(ip);
        }
        finally
        {
            _shouldIgnoreNextSelectionChange = false;
        }
    }

    private void UnselectPrevItem(object prevItem, object nextItem)
    {
        if (prevItem == null || prevItem == nextItem)
            return;

        try
        {
            _shouldIgnoreNextSelectionChange = true;
            ChangeSelectStatusForItem(prevItem, false);
        }
        finally
        {
            _shouldIgnoreNextSelectionChange = false;
        }
    }

    private void UndoSelectionAndRevertSelectionTo(object prevSelectedItem, object nextItem)
    {
        object selItem = null;
        if (prevSelectedItem != null)
        {
            if (IsSelectionSuppressed(prevSelectedItem))
            {
                AnimateSelectionChanged(null);
            }
            else
            {
                ChangeSelectStatusForItem(prevSelectedItem, true);
                AnimateSelectionChangedToItem(prevSelectedItem);
                selItem = prevSelectedItem;
            }
        }
        else
        {
            ChangeSelectStatusForItem(nextItem, false);
        }

        SelectedItem = selItem;
    }

    private void SelectOverflowItem(object item, IndexPath ip)
    {
        var itemBeingMoved = item;
        if (ip.GetSize() > 2)
            itemBeingMoved = GetItemFromIndex(_topNavRepeaterOverflowView, _topDataProvider.ConvertOriginalIndexToIndex(ip.GetAt(1)));

        var selOverflowItemIndex = _topDataProvider.IndexOf(itemBeingMoved);
        Debug.Assert(selOverflowItemIndex != _itemNotFound);
        var selOverflowItemWidth = _topDataProvider.GetWidthForItem(selOverflowItemIndex);

        var needInvalidMeasure = !_topDataProvider.IsValidWidthForItem(selOverflowItemIndex);

        if (!needInvalidMeasure)
        {
            var actWid = GetTopNavigationViewActualWidth;
            var desWid = MeasureTopNavigationViewDesiredWidth(Size.Infinity);
            Debug.Assert(desWid <= actWid);

            var widthAtLeastToBeRemoved = desWid + selOverflowItemWidth - actWid;

            var itemsToBeRemoved = FindMovableItemsToBeRemovedFromPrimaryList(widthAtLeastToBeRemoved, new List<int>(0));
            var topBeRemovedItemWidth = _topDataProvider.CalculateWidthForItems(itemsToBeRemoved);
            var widthAvailableToRecover = topBeRemovedItemWidth - widthAtLeastToBeRemoved;
            var itemsToBeAdded = FindMovableItemsRecoverToPrimaryList(widthAvailableToRecover, new[] { selOverflowItemIndex });

            itemsToBeAdded.Add(selOverflowItemIndex);

            _lastSelectedItemPendingAnimationInTopNav = itemBeingMoved;

            if (ip != IndexPath.Unselected && ip.GetSize() > 0)
            {
                if (itemsToBeRemoved.Any(t => ip.GetAt(1) == t))
                {
                    if (_activeIndicator != null)
                        AnimateSelectionChanged(null);
                }
            }

            if (_topDataProvider.HasInvalidWidth(itemsToBeAdded) ||
                NeedRearrangeOfTopElementsAfterOverflowSelectionChange(selOverflowItemIndex))
            {
                needInvalidMeasure = true;
            }
            else
            {
                _topDataProvider.MoveItemsToPrimaryList(itemsToBeAdded);
                _topDataProvider.MoveItemsOutOfPrimaryList(itemsToBeRemoved);
                SetSelectedItemAndExpectItemInvokeWhenSelectionChangedIfNotInvokedFromAPI(item);
                InvalidateMeasure();
            }
        }

        if (needInvalidMeasure)
        {
            _topDataProvider.MoveAllItemsToPrimaryList();
            SetSelectedItemAndExpectItemInvokeWhenSelectionChangedIfNotInvokedFromAPI(item);
            InvalidateTopNavPrimaryLayout();
        }
    }

    private void UpdateSelectionForMenuItems()
    {
        if (SelectedItem != null)
            return;

        var foundFirstSelected = UpdateSelectedItemFromMenuItems(_menuItems);
        UpdateSelectedItemFromMenuItems(_footerMenuItems, foundFirstSelected);
    }

    private bool UpdateSelectedItemFromMenuItems(IEnumerable menuItems, bool foundFirstSelected = false)
    {
        for (var i = 0; i < menuItems.Count(); i++)
        {
            if (menuItems.ElementAt(i) is FANavigationViewItem nvi && nvi.IsSelected)
            {
                if (!foundFirstSelected)
                {
                    try
                    {
                        _shouldIgnoreNextSelectionChange = true;
                        SelectedItem = nvi;
                        foundFirstSelected = true;
                    }
                    finally
                    {
                        _shouldIgnoreNextSelectionChange = false;
                    }
                }
                else
                {
                    nvi.IsSelected = false;
                }
            }
        }

        return foundFirstSelected;
    }


    /////////////////////////////////////////////////
    //////// NAVIGATIONVIEWITEM RELATED ////////////
    ///////////////////////////////////////////////

    private void SetNavigationViewItemBaseRevokers(FANavigationViewItemBase nvib)
    {
        var disp = new FACompositeDisposable(
            nvib.GetPropertyChangedObservable(IsVisibleProperty).Subscribe(OnNavigationViewItemBaseVisibilityPropertyChanged));

        nvib.SetValue(NavigationViewItemBaseRevokersProperty, disp);
    }

    private void SetNavigationViewItemRevokers(FANavigationViewItem nvi)
    {
        var revokers = nvi.GetValue(NavigationViewItemBaseRevokersProperty)
            ?? new FACompositeDisposable();
        nvi.SetValue(NavigationViewItemBaseRevokersProperty, revokers);

        revokers.Add(nvi.AddDisposableHandler(KeyDownEvent, OnNavigationViewItemKeyDown));
        revokers.Add(nvi.AddDisposableHandler(GotFocusEvent, OnNavigationViewItemGotFocus));
        revokers.Add(nvi.AddDisposableHandler(TappedEvent, OnNavigationViewItemTapped));
        revokers.Add(nvi.GetPropertyChangedObservable(ListBoxItem.IsSelectedProperty)
            .Subscribe(OnNavigationViewItemIsSelectedPropertyChanged));
        revokers.Add(nvi.GetPropertyChangedObservable(FANavigationViewItem.IsExpandedProperty)
            .Subscribe(OnNavigationViewItemExpandedPropertyChanged));
    }

    private void ClearNavigationViewItemBaseRevokers(FANavigationViewItemBase nvib)
    {
        var revokers = nvib.GetValue(NavigationViewItemBaseRevokersProperty);
        revokers?.Dispose();
        nvib.SetValue(NavigationViewItemBaseRevokersProperty, null);
    }

    private void OnNavigationViewItemIsSelectedPropertyChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Sender is not FANavigationViewItem nvi)
            return;

        var isContainerSelectedInModel = IsContainerTheSelectedItemInTheSelectionModel(nvi);
        var isSelectedInContainer = nvi.IsSelected;

        if (isSelectedInContainer && !isContainerSelectedInModel)
        {
            UpdateSelectionModelSelection(GetIndexPathForContainer(nvi));
        }
        else if (!isSelectedInContainer && isContainerSelectedInModel)
        {
            var indexPath = GetIndexPathForContainer(nvi);
            var indexPathFromModel = _selectionModel.SelectedIndex;

            if (indexPathFromModel != IndexPath.Unselected)
            {
                if (indexPath.CompareTo(indexPathFromModel) == 0)
                {
                    _selectionModel.DeselectAt(indexPath);
                }
                else if (!IsPaneOpen && indexPath.GetSize() == 0)
                {
                    UpdateIsChildSelected(indexPathFromModel, IndexPath.Unselected);
                    if (_prevIndicator == null && _nextIndicator == null && _activeIndicator != null)
                    {
                        ResetElementAnimationProperties(_activeIndicator, 0);
                        _activeIndicator = null;
                    }

                    _selectionModel.DeselectAt(indexPathFromModel);
                }
            }
        }

        if (isSelectedInContainer)
            nvi.IsChildSelected = false;
    }

    private void OnNavigationViewItemExpandedPropertyChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Sender is not FANavigationViewItem nvi)
            return;

        if (nvi.IsExpanded)
            RaiseExpandingEvent(nvi);

        ShowHideChildrenItemsRepeater(nvi);

        if (!nvi.IsExpanded)
            RaiseCollapsedEvent(nvi);
    }

    private void OnNavigationViewItemBaseVisibilityPropertyChanged(AvaloniaPropertyChangedEventArgs args)
        => UpdatePaneLayout();

    private void RaiseItemInvokedForNavigationViewItem(FANavigationViewItem nvi)
    {
        object nextItem = null;
        var prevItem = SelectedItem;
        var parentIR = GetParentItemsRepeaterForContainer(nvi);

        if (parentIR.ItemsSourceView != null)
        {
            var itemIndex = parentIR.GetElementIndex(nvi);
            if (itemIndex != -1)
                nextItem = parentIR.ItemsSourceView.GetAt(itemIndex);
        }

        var recDir = NavigationRecommendedTransitionDirection.Default;
        if (IsTopNavigationView && nvi.SelectsOnInvoked)
        {
            if (parentIR == _topNavRepeaterOverflowView)
            {
                recDir = NavigationRecommendedTransitionDirection.FromOverflow;
            }
            else if (prevItem != null)
            {
                recDir = GetRecommendedTransitionDirection(
                    NavigationViewItemBaseOrSettingsContentFromData(prevItem), nvi);
            }
        }

        RaiseItemInvoked(nextItem, nvi, recDir);
    }

    private void OnNavigationViewItemInvoked(FANavigationViewItem nvi)
    {
        _shouldRaiseItemInvokedAfterSelection = true;
        var selItem = SelectedItem;
        var updateSelection = _selectionModel != null && nvi.SelectsOnInvoked;

        if (updateSelection)
            UpdateSelectionModelSelection(GetIndexPathForContainer(nvi));

        if (selItem == SelectedItem)
            RaiseItemInvokedForNavigationViewItem(nvi);

        ToggleIsExpandedNavigationViewItem(nvi);
        ClosePaneIfNecessaryAfterItemIsClicked(nvi);

        if (updateSelection)
            CloseFlyoutIfRequired(nvi);
    }

    private void OnNavigationViewItemGotFocus(object sender, FocusChangedEventArgs e)
    {
        var nvi = (FANavigationViewItem)sender;

        if (!SelectionFollowsFocus || e.NavigationMethod == NavigationMethod.Pointer)
            return;

        if (!nvi.SelectsOnInvoked || nvi.IsSelected)
            return;

        if (IsTopNavigationView)
        {
            var parentIR = GetParentItemsRepeaterForContainer(nvi);
            if (parentIR != null && parentIR != _topNavRepeaterOverflowView)
                OnNavigationViewItemInvoked(nvi);
        }
        else
        {
            OnNavigationViewItemInvoked(nvi);
        }
    }

    private void OnNavigationViewItemKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is FANavigationViewItem nvi)
            HandleKeyEventForNavigationViewItem(nvi, args);
    }

    private void HandleKeyEventForNavigationViewItem(FANavigationViewItem nvi, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Enter:
            case Key.Space:
                args.Handled = true;
                OnNavigationViewItemInvoked(nvi);
                break;

            case Key.Home:
                args.Handled = true;
                KeyboardFocusFirstItemFromItem(nvi);
                break;

            case Key.End:
                args.Handled = true;
                KeyboardFocusLastItemFromItem(nvi);
                break;

            case Key.Right:
                if (IsTopNavigationView) FocusNextDownItem(nvi, args);
                break;

            case Key.Down:
                if (!IsTopNavigationView) FocusNextDownItem(nvi, args);
                break;

            case Key.Left:
                if (IsTopNavigationView) FocusNextUpItem(nvi, args);
                break;

            case Key.Up:
                if (!IsTopNavigationView) FocusNextUpItem(nvi, args);
                break;
        }
    }

    private FAItemsRepeater GetParentRootRepeaterForKeyboardNav(FANavigationViewItemBase nvib)
        => _lastItemExpandedIntoFlyout != null
            ? _lastItemExpandedIntoFlyout.GetRepeater
            : GetParentRootItemsRepeaterForContainer(nvib);

    private void KeyboardFocusFirstItemFromItem(FANavigationViewItemBase nvib)
    {
        if (GetFirstFocusableElement(GetParentRootRepeaterForKeyboardNav(nvib)) is Control c)
            c.Focus(NavigationMethod.Directional);
    }

    private void KeyboardFocusLastItemFromItem(FANavigationViewItemBase nvib)
    {
        if (GetLastFocusableElement(GetParentRootRepeaterForKeyboardNav(nvib)) is Control c)
            c.Focus(NavigationMethod.Directional);
    }

    private void FocusNextUpItem(FANavigationViewItem nvi, KeyEventArgs args)
    {
        if (args.Source != nvi)
            return;

        var options = new FindNextElementOptions { SearchRoot = TopLevel.GetTopLevel(this)?.Content as InputElement };
        var nextFocusableElement = TopLevel.GetTopLevel(this)?.FocusManager.FindNextElement(NavigationDirection.Up, options);

        if (nextFocusableElement is FANavigationViewItem nextNVI && nextNVI.Depth == nvi.Depth)
        {
            if (DoesNavigationViewItemHaveChildren(nextNVI))
            {
                if (nextNVI.GetRepeater is FAItemsRepeater ir)
                {
                    if (FocusManager.FindLastFocusableElement(ir) is Control lastFocusableElement)
                        args.Handled = lastFocusableElement.Focus(NavigationMethod.Directional);
                    else
                        args.Handled = nextNVI.Focus(NavigationMethod.Directional);
                }
            }
        }
    }

    private void FocusNextDownItem(FANavigationViewItem nvi, KeyEventArgs args)
    {
        if (args.Source != nvi || !DoesNavigationViewItemHaveChildren(nvi))
            return;

        if (nvi.GetRepeater is FAItemsRepeater ir)
        {
            var first = FocusManager.FindFirstFocusableElement(ir);
            if (first != null)
                args.Handled = first.Focus(NavigationMethod.Directional);
        }
    }

    private static Control GetFirstFocusableElement(FAItemsRepeater ir)
    {
        if (ir?.ItemsSourceView is not FAItemsSourceView isv)
            return null;

        for (var i = 0; i < isv.Count; i++)
        {
            if (ir.TryGetElement(i) is Control c && c.Focusable)
                return c;
        }

        return null;
    }

    private static Control GetLastFocusableElement(FAItemsRepeater ir)
    {
        if (ir?.ItemsSourceView is not FAItemsSourceView isv)
            return null;

        for (var i = isv.Count - 1; i >= 0; i--)
        {
            if (ir.TryGetElement(i) is Control c && c.Focusable)
                return c;
        }

        return null;
    }

    private void OnNavigationViewItemTapped(object sender, RoutedEventArgs e)
    {
        var nvi = (FANavigationViewItem)sender;
        OnNavigationViewItemInvoked(nvi);
        nvi.Focus();
        e.Handled = true;
    }

    private void ToggleIsExpandedNavigationViewItem(FANavigationViewItem nvi)
        => ChangeIsExpandedNavigationViewItem(nvi, !nvi.IsExpanded);

    private static void ChangeIsExpandedNavigationViewItem(FANavigationViewItem nvi, bool isExpanded)
    {
        if (nvi != null && (nvi.MenuItems?.Count > 0 || nvi.MenuItemsSource?.Count() > 0 || nvi.HasUnrealizedChildren))
            nvi.IsExpanded = isExpanded;
    }

    private void ShowHideChildrenItemsRepeater(FANavigationViewItem nvi)
    {
        nvi.ShowHideChildren();

        if (nvi.ShouldRepeaterShowInFlyout)
            _lastItemExpandedIntoFlyout = nvi.IsExpanded ? nvi : null;

        if (!nvi.IsSelected && nvi.IsChildSelected)
        {
            if (!nvi.IsRepeaterVisible)
                AnimateSelectionChanged(nvi);
            else
                AnimateSelectionChanged(FindLowestLevelContainerToDisplaySelectionIndicator());
        }

        nvi.RotateExpandCollapseChevron(nvi.IsExpanded);
    }

    private void RaiseExpandingEvent(FANavigationViewItemBase nvib)
    {
        ItemExpanding?.Invoke(this, new FANavigationViewItemExpandingEventArgs(this)
        {
            ExpandingItemContainer = nvib
        });
    }

    private void RaiseCollapsedEvent(FANavigationViewItemBase nvib)
    {
        ItemCollapsed?.Invoke(this, new FANavigationViewItemCollapsedEventArgs(this)
        {
            CollapsedItemContainer = nvib
        });
    }

    private void OnSelectedItemLayoutUpdated(object sender, EventArgs args)
    {
        if (!_isSelectionChangedPending)
            return;

        _isSelectionChangedPending = false;

        var item = _pendingSelectionChangedItem;
        var direction = _pendingSelectionChangedDirection;

        _pendingSelectionChangedItem = null;
        _pendingSelectionChangedDirection = NavigationRecommendedTransitionDirection.Default;

        (sender as Control).LayoutUpdated -= OnSelectedItemLayoutUpdated;

        var nvi = NavigationViewItemOrSettingsContentFromData(item);
        if (nvi != null)
            AnimateSelectionChanged(nvi);

        RaiseSelectionChangedEvent(item, direction);
    }


    ///////////////////////////////////////
    //////// LEFT NAV RELATED ////////////
    /////////////////////////////////////

    private void UpdateAdaptiveLayout(double width, bool forceSetDisplayMode = false)
    {
        if (IsTopNavigationView || _splitView == null)
            return;

        var dMode = FANavigationViewDisplayMode.Compact;
        var paneDisplayMode = PaneDisplayMode;

        if (paneDisplayMode == FANavigationViewPaneDisplayMode.Auto)
        {
            if (width >= ExpandedModeThresholdWidth) dMode = FANavigationViewDisplayMode.Expanded;
            else if (width > 0 && width < CompactModeThresholdWidth) dMode = FANavigationViewDisplayMode.Minimal;
        }
        else if (paneDisplayMode == FANavigationViewPaneDisplayMode.Left) dMode = FANavigationViewDisplayMode.Expanded;
        else if (paneDisplayMode == FANavigationViewPaneDisplayMode.LeftCompact) dMode = FANavigationViewDisplayMode.Compact;
        else if (paneDisplayMode == FANavigationViewPaneDisplayMode.LeftMinimal) dMode = FANavigationViewDisplayMode.Minimal;

        if (!forceSetDisplayMode && _initialNonForcedModeUpdate)
        {
            if (dMode == FANavigationViewDisplayMode.Minimal || dMode == FANavigationViewDisplayMode.Compact)
                ClosePane();

            _initialNonForcedModeUpdate = false;
        }

        var prev = DisplayMode;
        SetDisplayMode(dMode, forceSetDisplayMode);

        if (dMode == FANavigationViewDisplayMode.Expanded && IsPaneVisible && !_wasForceClosed)
            OpenPane();

        if (prev == FANavigationViewDisplayMode.Expanded && dMode == FANavigationViewDisplayMode.Compact)
            ClosePane();

        if (dMode == FANavigationViewDisplayMode.Minimal)
            ClosePane();
    }

    private void UpdatePaneLayout()
    {
        if (IsTopNavigationView || _itemsContainerRow == null || _menuItemsScrollViewer == null)
            return;

        var itemsContMargin = _itemsContainer?.Margin.Vertical() ?? 0d;
        var totalHeight = _itemsContainerRow.ActualHeight - itemsContMargin;

        if (totalHeight <= 0)
            return;

        var totalHeightHalf = totalHeight / 2;
        double heightForMenuItems;

        if (_footerItemsScrollViewer == null || _leftNavFooterMenuRepeater == null || _leftNavRepeater == null)
        {
            heightForMenuItems = totalHeightHalf;
            if (_footerItemsScrollViewer != null)
                _footerItemsScrollViewer.MaxHeight = totalHeightHalf;
        }
        else
        {
            var footerItemsRepeaterTopBottomMargin = _leftNavFooterMenuRepeater.IsVisible
                ? _leftNavFooterMenuRepeater.Margin.Vertical() : 0d;
            var footerDesiredHeight = footerItemsRepeaterTopBottomMargin +
                LayoutHelper.MeasureChild(_leftNavFooterMenuRepeater, Size.Infinity, default).Height;

            var paneFooterActualHeight = 0d;
            if (_leftNavFooterContentBorder != null)
            {
                var paneFooterTopBottomMargin = _leftNavFooterContentBorder.IsVisible
                    ? _leftNavFooterContentBorder.Margin.Vertical() : 0d;
                paneFooterActualHeight = _leftNavFooterContentBorder.Bounds.Height + paneFooterTopBottomMargin;
            }

            var menuItemsDesiredHeight = _leftNavRepeater.DesiredSize.Height;
            var menuItemsActualHeight = _leftNavRepeater.Bounds.Height +
                (_leftNavRepeater.IsVisible ? _leftNavRepeater.Margin.Vertical() : 0);
            var footerGroupDesiredHeight = footerDesiredHeight + paneFooterActualHeight;

            if (_footerItemsSource.Count == 0)
            {
                PseudoClasses.Set(s_pcSeparator, false);
                _menuItemsScrollViewer.MaxHeight = totalHeight;
                return;
            }

            if (_menuItemsSource.Count == 0)
            {
                _footerItemsScrollViewer.MaxHeight = totalHeight;
                PseudoClasses.Set(s_pcSeparator, false);
                _menuItemsScrollViewer.MaxHeight = 0d;
                return;
            }

            if (totalHeight >= menuItemsDesiredHeight + footerGroupDesiredHeight)
            {
                _footerItemsScrollViewer.MaxHeight = footerDesiredHeight;
                PseudoClasses.Set(s_pcSeparator, false);
                heightForMenuItems = totalHeight - footerDesiredHeight;
            }
            else if (menuItemsDesiredHeight <= totalHeightHalf)
            {
                _footerItemsScrollViewer.MaxHeight = totalHeight - menuItemsActualHeight;
                PseudoClasses.Set(s_pcSeparator, true);
                heightForMenuItems = menuItemsActualHeight;
            }
            else if (footerDesiredHeight <= totalHeightHalf)
            {
                _footerItemsScrollViewer.MaxHeight = footerDesiredHeight;
                PseudoClasses.Set(s_pcSeparator, true);
                heightForMenuItems = totalHeight - footerDesiredHeight;
            }
            else
            {
                _footerItemsScrollViewer.MaxHeight = totalHeightHalf;
                PseudoClasses.Set(s_pcSeparator, true);
                heightForMenuItems = totalHeightHalf;
            }
        }

        _menuItemsScrollViewer.MaxHeight = heightForMenuItems;
    }

    private void OnPaneTitleHolderSizeChanged(Rect r) => UpdateBackAndCloseButtonsVisibility();

    private void OpenPane()
    {
        try
        {
            _isOpenPaneForInteraction = true;
            IsPaneOpen = true;
        }
        finally
        {
            _isOpenPaneForInteraction = false;
        }
    }

    private void ClosePane()
    {
        try
        {
            _isOpenPaneForInteraction = true;
            IsPaneOpen = false;
        }
        finally
        {
            _isOpenPaneForInteraction = false;
        }
    }

    private bool AttemptClosePaneLightly()
    {
        var ea = new FANavigationViewPaneClosingEventArgs();
        PaneClosing?.Invoke(this, ea);

        if (!ea.Cancel || _wasForceClosed)
        {
            _blockNextClosingEvent = true;
            ClosePane();
            return true;
        }

        return false;
    }

    private void UpdatePaneTabFocusNavigation()
    {
        // Reserved for future XYKeyboardFocus support
    }

    private void ClosePaneIfNecessaryAfterItemIsClicked(FANavigationViewItem item)
    {
        if (IsPaneOpen &&
            DisplayMode != FANavigationViewDisplayMode.Expanded &&
            !DoesNavigationViewItemHaveChildren(item) &&
            !_shouldIgnoreNextSelectionChange)
        {
            ClosePane();
        }
    }


    //////////////////////////////////////
    //////// TOP NAV RELATED ////////////
    ////////////////////////////////////

    private void InvalidateTopNavPrimaryLayout()
    {
        if (_appliedTemplate && IsTopNavigationView)
            InvalidateMeasure();
    }

    private void ResetAndRearrangeTopNavItems(Size availableSize)
    {
        if (HasTopNavigationViewItemNotInPrimaryList())
            _topDataProvider.MoveAllItemsToPrimaryList();

        ArrangeTopNavItems(availableSize);
    }

    private void HandleTopNavigationMeasureOverride(Size availableSize)
    {
        if (HasTopNavigationViewItemNotInPrimaryList())
            HandleTopNavigationMeasureOverrideOverflow(availableSize);
        else
            HandleTopNavigationMeasureOverrideNormal(availableSize);

        if (_topNavigationMode == TopNavigationViewLayoutState.Uninitialized)
            _topNavigationMode = TopNavigationViewLayoutState.Initialized;
    }

    private void HandleTopNavigationMeasureOverrideNormal(Size availableSize)
    {
        if (MeasureTopNavigationViewDesiredWidth(Size.Infinity) > availableSize.Width)
            ResetAndRearrangeTopNavItems(availableSize);
    }

    private void HandleTopNavigationMeasureOverrideOverflow(Size availableSize)
    {
        var desWidth = MeasureTopNavigationViewDesiredWidth(Size.Infinity);
        if (desWidth > availableSize.Width)
        {
            ShrinkTopNavigationSize(desWidth, availableSize);
        }
        else if (desWidth < availableSize.Width)
        {
            var fullyRecoverWidth = _topDataProvider.WidthRequiredToRecoveryAllItemsToPrimary();
            if (availableSize.Width >= desWidth + fullyRecoverWidth + _topNavigationRecoveryGracePeriodWidth)
            {
                ResetAndRearrangeTopNavItems(availableSize);
            }
            else
            {
                var moveItems = FindMovableItemsRecoverToPrimaryList(availableSize.Width - desWidth, new List<int>(0));
                _topDataProvider.MoveItemsToPrimaryList(moveItems);
            }
        }
    }

    private void ArrangeTopNavItems(Size availableSize)
    {
        SetOverflowButtonVisibility(false);
        var desWidth = MeasureTopNavigationViewDesiredWidth(Size.Infinity);
        if (!(desWidth < availableSize.Width))
        {
            SetOverflowButtonVisibility(true);
            var desWidthForOB = MeasureTopNavigationViewDesiredWidth(Size.Infinity);
            _topDataProvider.OverflowButtonWidth = desWidthForOB - desWidth;
            ShrinkTopNavigationSize(desWidthForOB, availableSize);
        }
    }

    private bool NeedRearrangeOfTopElementsAfterOverflowSelectionChange(int selOriginalIndex)
    {
        _topDataProvider.GetPrimaryItems();
        var primaryListSize = _topDataProvider.PrimaryListSize;
        var indexInPrimary = _topDataProvider.ConvertOriginalIndexToIndex(selOriginalIndex);

        if (indexInPrimary >= primaryListSize - 1)
            return false;

        var nextIndexInPrimary = indexInPrimary + 1;
        var nextIndexInOriginal = selOriginalIndex + 1;
        var prevIndexInOriginal = selOriginalIndex - 1;

        if (indexInPrimary > 0)
        {
            var prev = _topDataProvider.ConvertPrimaryIndexToIndex(new[] { nextIndexInPrimary - 1 });
            if (prev[0] != prevIndexInOriginal)
                return true;
        }

        while (nextIndexInPrimary < primaryListSize)
        {
            var originalIndex = _topDataProvider.ConvertPrimaryIndexToIndex(new[] { nextIndexInPrimary });
            if (nextIndexInOriginal != originalIndex[0])
                return true;
            nextIndexInPrimary++;
            nextIndexInOriginal++;
        }

        return false;
    }

    private void ShrinkTopNavigationSize(double desWidth, Size availableSize)
    {
        UpdateTopNavigationWidthCache();

        var selItemIndex = SelectedItemIndex;

        var possibleWidthForPrimaryList = MeasureTopNavMenuItemsHostDesiredWidth(Size.Infinity) - (desWidth - availableSize.Width);
        if (possibleWidthForPrimaryList >= 0)
        {
            var itemToBeRemoved = FindMovableItemsBeyondAvailableWidth(possibleWidthForPrimaryList);
            KeepAtLeastOneItemInPrimaryList(itemToBeRemoved, true);
            _topDataProvider.MoveItemsOutOfPrimaryList(itemToBeRemoved);
        }

        desWidth = MeasureTopNavigationViewDesiredWidth(Size.Infinity);
        var widthAtLeastToBeRemoved = desWidth - availableSize.Width;
        if (widthAtLeastToBeRemoved > 0)
        {
            var itemToBeRemoved = FindMovableItemsToBeRemovedFromPrimaryList(widthAtLeastToBeRemoved, new[] { selItemIndex });
            KeepAtLeastOneItemInPrimaryList(itemToBeRemoved, false);
            _topDataProvider.MoveItemsOutOfPrimaryList(itemToBeRemoved);
        }
    }

    private IList<int> FindMovableItemsRecoverToPrimaryList(double availableWidth, IList<int> includeItems)
    {
        var toBeMoved = new List<int>(includeItems.Count + 4);
        var size = _topDataProvider.Size;

        for (var index = 0; index < includeItems.Count; index++)
        {
            toBeMoved.Add(includeItems[index]);
            availableWidth -= _topDataProvider.GetWidthForItem(includeItems[index]);
        }

        var i = 0;
        while (i < size && availableWidth > 0)
        {
            if (!_topDataProvider.IsItemInPrimaryList(i) && !includeItems.Contains(i))
            {
                var wid = _topDataProvider.GetWidthForItem(i);
                if (availableWidth >= wid)
                {
                    toBeMoved.Add(i);
                    availableWidth -= wid;
                }
                else
                {
                    break;
                }
            }
            i++;
        }

        if (i == size && toBeMoved.Count > 0)
            toBeMoved.RemoveAt(toBeMoved.Count - 1);

        return toBeMoved;
    }

    private IList<int> FindMovableItemsToBeRemovedFromPrimaryList(double widthAtLeastToBeRemoved, IList<int> excludeItems)
    {
        var toBeMoved = new List<int>();
        var i = _topDataProvider.Size - 1;
        while (i >= 0 && widthAtLeastToBeRemoved > 0)
        {
            if (_topDataProvider.IsItemInPrimaryList(i) && !excludeItems.Contains(i))
            {
                toBeMoved.Add(i);
                widthAtLeastToBeRemoved -= _topDataProvider.GetWidthForItem(i);
            }
            i--;
        }

        return toBeMoved;
    }

    private IList<int> FindMovableItemsBeyondAvailableWidth(double availableWidth)
    {
        var toBeMoved = new List<int>();
        if (_topNavRepeater != null)
        {
            var selItemIndexInPrimary = _topDataProvider.IndexOf(SelectedItem, NavigationViewSplitVectorID.PrimaryList);
            var size = _topDataProvider.PrimaryListSize;
            double requiredWidth = 0;

            for (var i = 0; i < size; i++)
            {
                if (i == selItemIndexInPrimary)
                    continue;

                var shouldMove = true;
                if (requiredWidth <= availableWidth)
                {
                    var cont = _topNavRepeater.TryGetElement(i);
                    if (cont != null)
                    {
                        requiredWidth += cont.DesiredSize.Width;
                        shouldMove = requiredWidth > availableWidth;
                    }
                }

                if (shouldMove)
                    toBeMoved.Add(i);
            }
        }

        return _topDataProvider.ConvertPrimaryIndexToIndex(toBeMoved);
    }

    private void KeepAtLeastOneItemInPrimaryList(IList<int> itemInPrimaryToBeRemoved, bool shouldKeepFirst)
    {
        if (itemInPrimaryToBeRemoved.Count > 0 && itemInPrimaryToBeRemoved.Count == _topDataProvider.PrimaryListSize)
        {
            if (shouldKeepFirst)
                itemInPrimaryToBeRemoved.RemoveAt(0);
            else
                itemInPrimaryToBeRemoved.RemoveAt(itemInPrimaryToBeRemoved.Count - 1);
        }
    }

    private void UpdateTopNavigationWidthCache()
    {
        var size = _topDataProvider.PrimaryListSize;
        if (_topNavRepeater != null)
        {
            for (var i = 0; i < size; i++)
            {
                if (_topNavRepeater.TryGetElement(i) is Control c)
                    _topDataProvider.UpdateWidthForPrimaryItem(i, c.DesiredSize.Width);
                else
                    break;
            }
        }
    }


    ////////////////////////////////////////
    //////// SPLITVIEW RELATED ////////////
    //////////////////////////////////////

    private void OnSplitViewClosedCompactChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == SplitView.IsPaneOpenProperty || args.Property == SplitView.DisplayModeProperty)
            UpdateIsClosedCompact();
    }

    private void OnSplitViewPaneClosed(object sender, RoutedEventArgs e)
    {
        if (e.Source == _splitView)
            PaneClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSplitViewPaneClosing(object sender, CancelRoutedEventArgs e)
    {
        if (e.Source != _splitView)
            return;

        var pendingCancel = false;

        if (!_blockNextClosingEvent)
        {
            var ea = new FANavigationViewPaneClosingEventArgs { SplitViewClosingArgs = e };
            PaneClosing?.Invoke(this, ea);
            pendingCancel = ea.Cancel;
        }
        else
        {
            _blockNextClosingEvent = false;
        }

        if (pendingCancel || _splitView == null || _leftNavRepeater == null)
            return;

        var isCompact = _splitView.DisplayMode == SplitViewDisplayMode.CompactInline ||
                        _splitView.DisplayMode == SplitViewDisplayMode.CompactOverlay;

        PseudoClasses.Set(s_pcListSizeCompact, isCompact);

        if (isCompact)
            UpdatePaneToggleSize();
    }

    private void OnSplitViewPaneOpened(object sender, RoutedEventArgs e)
    {
        if (e.Source == _splitView)
            PaneOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnSplitViewPaneOpening(object sender, RoutedEventArgs e)
    {
        if (e.Source != _splitView)
            return;

        if (_leftNavRepeater != null)
            PseudoClasses.Set(s_pcListSizeCompact, false);

        PaneOpening?.Invoke(this, EventArgs.Empty);
    }


    ///////////////////////////////////
    //////// PANE BUTTONS ////////////
    /////////////////////////////////

    private void OnPaneToggleButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsPaneOpen)
        {
            _wasForceClosed = true;
            ClosePane();
        }
        else
        {
            _wasForceClosed = false;
            OpenPane();
        }
    }

    private void OnPaneSearchButtonClick(object sender, RoutedEventArgs e)
    {
        _wasForceClosed = false;
        OpenPane();
        AutoCompleteBox?.Focus(NavigationMethod.Tab);
    }

    private void OnBackButtonClicked(object sender, RoutedEventArgs e)
        => BackRequested?.Invoke(this, new FANavigationViewBackRequestedEventArgs());

    private void UpdatePaneButtonWidths()
    {
        TemplateSettings.PaneToggleButtonWidth = CompactPaneLength;
        TemplateSettings.SmallerPaneToggleButtonWidth = CompactPaneLength - 8;
    }

    private void UpdatePaneToggleButtonVisibility()
        => TemplateSettings.PaneToggleButtonVisibility = IsPaneToggleButtonVisible && !IsTopNavigationView;

    private void UpdatePaneToggleSize()
    {
        if (_splitView == null)
            return;

        var toggleWidth = TemplateSettings.PaneToggleButtonWidth;

        if (!_isClosedCompact && !string.IsNullOrEmpty(PaneTitle))
        {
            if (_splitView.DisplayMode == SplitViewDisplayMode.Overlay && IsPaneOpen)
            {
                toggleWidth = OpenPaneLength - ((ShouldShowBackButton || ShouldShowCloseButton) ? _backButtonWidth : 0);
            }
            else if (!(_splitView.DisplayMode == SplitViewDisplayMode.Overlay && !IsPaneOpen))
            {
                toggleWidth = OpenPaneLength;
            }
        }

        if (_paneToggleButton != null)
            _paneToggleButton.Width = toggleWidth;
    }

    private void UpdateBackAndCloseButtonsVisibility()
    {
        if (!_appliedTemplate)
            return;

        var showBack = ShouldShowBackButton;
        var vsdm = GetVisualStateDisplayMode(DisplayMode);
        var useLeftPadding =
            (vsdm == NavigationViewVisualStateDisplayMode.Minimal && !IsTopNavigationView) ||
            vsdm == NavigationViewVisualStateDisplayMode.MinimalWithBackButton;
        double leftPadding = 0;
        double paneHeaderPaddingForToggle = 0;
        double paneHeaderPaddingForClose = 0;
        double paneHeaderContentBorderRowMinHeight = 0;

        TemplateSettings.BackButtonVisibility = showBack;

        if (_paneToggleButton != null && IsPaneToggleButtonVisible)
        {
            paneHeaderContentBorderRowMinHeight = GetPaneToggleButtonHeight();
            paneHeaderPaddingForToggle = GetPaneToggleButtonWidth();

            if (useLeftPadding)
                leftPadding = paneHeaderPaddingForToggle;
        }

        if (_backButton != null && useLeftPadding && showBack)
            leftPadding += _backButton.Width;

        if (_closeButton != null)
        {
            _closeButton.IsVisible = ShouldShowCloseButton;

            if (ShouldShowCloseButton)
            {
                paneHeaderContentBorderRowMinHeight = Math.Max(paneHeaderContentBorderRowMinHeight, _closeButton.Height);
                if (useLeftPadding)
                {
                    paneHeaderPaddingForClose = _closeButton.Width;
                    leftPadding += paneHeaderPaddingForClose;
                }
            }
        }

        if (_contentLeftPadding != null)
            _contentLeftPadding.Width = leftPadding;

        if (_paneHeaderToggleButtonColumn != null)
            _paneHeaderToggleButtonColumn.Width = new GridLength(paneHeaderPaddingForToggle);

        if (_paneHeaderCloseButtonColumn != null)
            _paneHeaderCloseButtonColumn.Width = new GridLength(paneHeaderPaddingForClose);

        if (_paneTitleHolderFrameworkElement?.IsVisible == true &&
            paneHeaderContentBorderRowMinHeight == 0)
        {
            paneHeaderContentBorderRowMinHeight = _paneTitleHolderFrameworkElement.Bounds.Height;
        }

        if (_paneHeaderContentBorderRow != null)
            _paneHeaderContentBorderRow.MinHeight = paneHeaderContentBorderRowMinHeight;

        if (_paneContentGrid != null && _paneContentGrid.RowDefinitions.Count >= _backButtonRowDefinition)
        {
            var backButtonRowHeight = 0;
            if (!IsOverlay && showBack)
                backButtonRowHeight = _backButtonHeight;
            else if (_backButton == null)
                backButtonRowHeight = c_toggleButtonHeightWithNoBackButton;

            _paneContentGrid.RowDefinitions[_backButtonRowDefinition].Height = new GridLength(backButtonRowHeight);
        }

        PseudoClasses.Set(s_pcBackButtonCollapsed, !showBack);
        UpdateTitleBarPadding();
    }


    /////////////////////////////////////////////////////////
    //////// DISPLAYMODE & VISUAL STATE RELATED ////////////
    ///////////////////////////////////////////////////////

    private void SetDisplayMode(FANavigationViewDisplayMode dMode, bool forceSetDisplayMode = false)
    {
        UpdateVisualStateForDisplayModeGroup(dMode);

        if (forceSetDisplayMode || DisplayMode != dMode)
        {
            UpdateHeaderVisibility(dMode);
            UpdatePaneTabFocusNavigation();
            UpdatePaneToggleSize();
            RaiseDisplayModeChanged(dMode);
        }
    }

    private NavigationViewVisualStateDisplayMode GetVisualStateDisplayMode(FANavigationViewDisplayMode dMode)
    {
        var pdm = PaneDisplayMode;

        if (IsTopNavigationView)
            return NavigationViewVisualStateDisplayMode.Minimal;

        if (pdm == FANavigationViewPaneDisplayMode.Left ||
            (pdm == FANavigationViewPaneDisplayMode.Auto && dMode == FANavigationViewDisplayMode.Expanded))
            return NavigationViewVisualStateDisplayMode.Expanded;

        if (pdm == FANavigationViewPaneDisplayMode.LeftCompact ||
            (pdm == FANavigationViewPaneDisplayMode.Auto && dMode == FANavigationViewDisplayMode.Compact))
            return NavigationViewVisualStateDisplayMode.Compact;

        if (ShouldShowBackButton || ShouldShowCloseButton)
            return NavigationViewVisualStateDisplayMode.MinimalWithBackButton;

        return NavigationViewVisualStateDisplayMode.Minimal;
    }

    private void UpdateVisualStateForDisplayModeGroup(FANavigationViewDisplayMode dMode)
    {
        if (_splitView == null)
            return;

        var vsdm = GetVisualStateDisplayMode(dMode);
        var svdm = SplitViewDisplayMode.Overlay;

        switch (vsdm)
        {
            case NavigationViewVisualStateDisplayMode.MinimalWithBackButton:
                PseudoClasses.Set(s_pcMinimalWithBack, true);
                PseudoClasses.Set(s_pcMinimal, false);
                PseudoClasses.Set(s_pcCompact, false);
                PseudoClasses.Set(s_pcExpanded, false);
                svdm = SplitViewDisplayMode.Overlay;
                break;

            case NavigationViewVisualStateDisplayMode.Minimal:
                PseudoClasses.Set(s_pcMinimalWithBack, false);
                PseudoClasses.Set(s_pcMinimal, true);
                PseudoClasses.Set(s_pcCompact, false);
                PseudoClasses.Set(s_pcExpanded, false);
                svdm = SplitViewDisplayMode.Overlay;
                break;

            case NavigationViewVisualStateDisplayMode.Compact:
                PseudoClasses.Set(s_pcMinimalWithBack, false);
                PseudoClasses.Set(s_pcMinimal, false);
                PseudoClasses.Set(s_pcCompact, true);
                PseudoClasses.Set(s_pcExpanded, false);
                svdm = SplitViewDisplayMode.CompactOverlay;
                break;

            case NavigationViewVisualStateDisplayMode.Expanded:
                PseudoClasses.Set(s_pcMinimalWithBack, false);
                PseudoClasses.Set(s_pcMinimal, false);
                PseudoClasses.Set(s_pcCompact, false);
                PseudoClasses.Set(s_pcExpanded, true);
                svdm = SplitViewDisplayMode.CompactInline;
                break;
        }

        // Reset top-nav pseudo class in all non-top modes
        PseudoClasses.Set(s_pcTopNavMinimal, IsTopNavigationView);

        if (IsTopNavigationView)
        {
            PseudoClasses.Set(s_pcMinimalWithBack, false);
            PseudoClasses.Set(s_pcMinimal, false);
            PseudoClasses.Set(s_pcCompact, false);
            PseudoClasses.Set(s_pcExpanded, false);
        }

        if (!IsPaneVisible)
            svdm = SplitViewDisplayMode.CompactOverlay;

        if (_fromOnApplyTemplate)
            _updateVisualStateForDisplayModeFromOnLoaded = true;
        else
            _splitView.DisplayMode = svdm;
    }

    private void UpdateVisualState()
    {
        if (!_appliedTemplate)
            return;

        PseudoClasses.Set(s_pcAutoSuggestCollapsed, AutoCompleteBox == null);

        if (!IsTopNavigationView)
            PseudoClasses.Set(s_pcPaneToggleCollapsed, !IsPaneToggleButtonVisible || _isLeftPaneTitleEmpty);
    }

    private void RaiseDisplayModeChanged(FANavigationViewDisplayMode mode)
    {
        DisplayMode = mode;
        DisplayModeChanged?.Invoke(this, new FANavigationViewDisplayModeChangedEventArgs(mode));
    }

    private void UpdatePaneOverlayGroup()
    {
        if (_splitView == null)
            return;

        var overlaying = IsPaneOpen &&
            (_splitView.DisplayMode == SplitViewDisplayMode.CompactOverlay ||
             _splitView.DisplayMode == SplitViewDisplayMode.Overlay);

        PseudoClasses.Set(s_pcPaneNotOverlaying, !overlaying);
    }


    //////////////////////////////////////////
    //////// SELECTION INDICATOR ////////////
    ////////////////////////////////////////

    private void AnimateSelectionChangedToItem(object selItem)
    {
        if (selItem != null && !IsSelectionSuppressed(selItem))
            AnimateSelectionChanged(selItem);
    }

    private void AnimateSelectionChanged(object nextItem)
    {
        if (_lastSelectedItemPendingAnimationInTopNav != null || !_appliedTemplate)
            return;

        var prevIndicator = _activeIndicator;
        var nextIndicator = FindSelectionIndicator(nextItem);

        if (_activeIndicator == null && nextItem != null && nextIndicator == null)
            Dispatcher.UIThread.Post(() => AnimateSelectionChanged(nextItem));

        var haveValidAnimation = false;
        if (_prevIndicator != null || _nextIndicator != null)
        {
            if (nextIndicator != null && _nextIndicator == nextIndicator)
            {
                if (prevIndicator != null && _prevIndicator == null)
                    ResetElementAnimationProperties(prevIndicator, 0f);
                haveValidAnimation = true;
            }
            else
            {
                OnAnimationComplete();
            }
        }

        if (haveValidAnimation)
            return;

        if (prevIndicator != nextIndicator && _paneContentGrid != null &&
            prevIndicator != null && nextIndicator != null && FAUISettings.AreAnimationsEnabled())
        {
            ResetElementAnimationProperties(prevIndicator, 1f);
            ResetElementAnimationProperties(nextIndicator, 1f);

            var t1 = prevIndicator.TransformToVisual(_paneContentGrid) ?? Matrix.Identity;
            var t2 = nextIndicator.TransformToVisual(_paneContentGrid) ?? Matrix.Identity;
            var prevPosPoint = t1.Transform(default);
            var nextPosPoint = t2.Transform(default);
            var prevSize = prevIndicator.Bounds.Size;
            var nextSize = nextIndicator.Bounds.Size;

            var areElementsAtSameDepth = IsTopNavigationView
                ? prevPosPoint.Y == nextPosPoint.Y
                : prevPosPoint.X == nextPosPoint.X;

            if (!areElementsAtSameDepth)
            {
                var isNextBelow = prevPosPoint.Y < nextPosPoint.Y;
                if (prevIndicator.Bounds.Height > prevIndicator.Bounds.Width)
                    PlayIndicatorNonSameLevelAnimations(prevIndicator, true, !isNextBelow);
                else
                    PlayIndicatorNonSameLevelTopPrimaryAnimation(prevIndicator, true);

                if (nextIndicator.Bounds.Height > nextIndicator.Bounds.Width)
                    PlayIndicatorNonSameLevelAnimations(nextIndicator, false, isNextBelow);
                else
                    PlayIndicatorNonSameLevelTopPrimaryAnimation(nextIndicator, false);
            }
            else
            {
                var prevPos = IsTopNavigationView ? prevPosPoint.X : prevPosPoint.Y;
                var nextPos = IsTopNavigationView ? nextPosPoint.X : nextPosPoint.Y;
                var outgoingEndPosition = nextPos - prevPos;
                var incomingStartPosition = prevPos - nextPos;

                PlayIndicatorAnimations(prevIndicator, 0, outgoingEndPosition, prevSize, nextSize, true);
                PlayIndicatorAnimations(nextIndicator, incomingStartPosition, 0, prevSize, nextSize, false);
            }

            _prevIndicator = prevIndicator;
            _nextIndicator = nextIndicator;

            DispatcherTimer.RunOnce(OnAnimationComplete, TimeSpan.FromMilliseconds(700), DispatcherPriority.Render);
        }
        else if (prevIndicator != nextIndicator)
        {
            ResetElementAnimationProperties(prevIndicator, 0f);
            ResetElementAnimationProperties(nextIndicator, 1f);
        }

        _activeIndicator = nextIndicator;
    }

    private static void PlayIndicatorNonSameLevelAnimations(Control indicator, bool isOutgoing, bool fromTop)
    {
        var visual = ElementComposition.GetElementVisual(indicator);
        if (visual == null)
            return;

        var comp = visual.Compositor;
        double beginScale = isOutgoing ? 1 : 0;
        double endScale = isOutgoing ? 0 : 1;

        var scaleAnim = comp.CreateVector3DKeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0f, new Vector3D(1, beginScale, 1));
        scaleAnim.InsertKeyFrame(1f, new Vector3D(1, endScale, 1));
        scaleAnim.Duration = TimeSpan.FromMilliseconds(600);

        var size = indicator.Bounds.Size;
        var dimension = IsTopNav(indicator) ? size.Width : size.Height;
        var newCenter = fromTop ? 0 : dimension;

        visual.CenterPoint = new Vector3D(visual.CenterPoint.X, newCenter, visual.CenterPoint.Z);
        visual.StartAnimation("Scale", scaleAnim);

        if (isOutgoing)
        {
            var opacityAnim = comp.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0.0f, 1.0f);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(600);
            visual.StartAnimation("Opacity", opacityAnim);
        }
    }

    private static bool IsTopNav(Control c)
        => c.FindAncestorOfType<FANavigationView>(true)?.IsTopNavigationView ?? false;

    private void PlayIndicatorNonSameLevelTopPrimaryAnimation(Control indicator, bool isOutgoing)
    {
        var visual = ElementComposition.GetElementVisual(indicator);
        if (visual == null)
            return;

        var comp = visual.Compositor;
        double beginScale = isOutgoing ? 1 : 0;
        double endScale = isOutgoing ? 0 : 1;

        var scaleAnim = comp.CreateVector3DKeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0, new Vector3D(beginScale, visual.Scale.Y, visual.Scale.Z));
        scaleAnim.InsertKeyFrame(1, new Vector3D(endScale, visual.Scale.Y, visual.Scale.Z));
        scaleAnim.Duration = TimeSpan.FromMilliseconds(600);

        var newCenter = indicator.Bounds.Size.Width / 2;
        visual.CenterPoint = new Vector3D(visual.CenterPoint.X, newCenter, visual.CenterPoint.Z);
        visual.StartAnimation("Scale", scaleAnim);
    }

    private void PlayIndicatorAnimations(Control indicator, double from, double to,
        Size beginSize, Size endSize, bool isOutgoing)
    {
        var visual = ElementComposition.GetElementVisual(indicator);
        if (visual == null)
            return;

        var comp = visual.Compositor;
        var size = indicator.Bounds.Size;
        var dimension = IsTopNavigationView ? size.Width : size.Height;

        double beginScale = 1f;
        double endScale = 1f;
        if (IsTopNavigationView && Math.Abs(size.Width) > 0.001)
        {
            beginScale = beginSize.Width / size.Width;
            endScale = endSize.Width / size.Width;
        }

        var easing1 = new SplineEasing(0.9, 0.1, 1, 0.2);
        var easing2 = new SplineEasing(0.1, 0.9, 0.2, 1.0);
        var step = new StepEasingFunction { Steps = 5 };

        if (isOutgoing)
        {
            var opacityAnim = comp.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0.0f, 1.0f);
            opacityAnim.InsertKeyFrame(0.333f, 1.0f, step);
            opacityAnim.InsertKeyFrame(1.0f, 0.0f, easing2);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(600);
            visual.StartAnimation("Opacity", opacityAnim);
        }

        var posAnim = comp.CreateVector3DKeyFrameAnimation();
        var scaleAnim = comp.CreateVector3DKeyFrameAnimation();
        var centerAnim = comp.CreateVector3DKeyFrameAnimation();

        if (!IsTopNavigationView)
        {
            posAnim.InsertKeyFrame(0.0f, new Vector3D(visual.Offset.X,
                from < to ? from : from + dimension * (beginScale - 1), visual.Offset.Z));
            posAnim.InsertKeyFrame(0.333f, new Vector3D(visual.Offset.X,
                from < to ? to + dimension * (endScale - 1) : to, visual.Offset.Z), step);
            posAnim.Duration = TimeSpan.FromMilliseconds(600);

            scaleAnim.InsertKeyFrame(0.0f, new Vector3D(1, beginScale, 1));
            scaleAnim.InsertKeyFrame(0.333f, new Vector3D(1,
                Math.Abs(to - from) / dimension + (from < to ? endScale : beginScale), 1), easing1);
            scaleAnim.InsertKeyFrame(1.0f, new Vector3D(1, endScale, endScale), easing2);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(600);

            centerAnim.InsertKeyFrame(0.0f, new Vector3D(visual.CenterPoint.X, from < to ? 0f : dimension, visual.CenterPoint.Z));
            centerAnim.InsertKeyFrame(1.0f, new Vector3D(visual.CenterPoint.X, from < to ? dimension : 0f, visual.CenterPoint.Z), step);
        }
        else
        {
            posAnim.InsertKeyFrame(0.0f, new Vector3D(
                from < to ? from : from + dimension * (beginScale - 1), visual.Offset.Y, visual.Offset.Z));
            posAnim.InsertKeyFrame(0.333f, new Vector3D(
                from < to ? to + dimension * (endScale - 1) : to, visual.Offset.Y, visual.Offset.Z), step);
            posAnim.Duration = TimeSpan.FromMilliseconds(600);

            scaleAnim.InsertKeyFrame(0.0f, new Vector3D(beginScale, 1, 1));
            scaleAnim.InsertKeyFrame(0.333f, new Vector3D(
                Math.Abs(to - from) / dimension + (from < to ? endScale : beginScale), 1, 1), easing1);
            scaleAnim.InsertKeyFrame(1.0f, new Vector3D(1, endScale, endScale), easing2);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(600);

            centerAnim.InsertKeyFrame(0.0f, new Vector3D(from < to ? 0f : dimension, visual.CenterPoint.Y, visual.CenterPoint.Z));
            centerAnim.InsertKeyFrame(1.0f, new Vector3D(from < to ? dimension : 0f, visual.CenterPoint.Y, visual.CenterPoint.Z), step);
        }

        centerAnim.Duration = TimeSpan.FromMilliseconds(200);

        visual.StartAnimation("Offset", posAnim);
        visual.StartAnimation("Scale", scaleAnim);
        visual.StartAnimation("CenterPoint", centerAnim);
    }

    private void OnAnimationComplete()
    {
        var indicator = _prevIndicator;
        ResetElementAnimationProperties(indicator, 0f);
        _prevIndicator = null;

        indicator = _nextIndicator;
        ResetElementAnimationProperties(indicator, 1);
        _nextIndicator = null;
    }

    private static void ResetElementAnimationProperties(Control element, double desiredOpacity)
    {
        if (element == null)
            return;

        element.Opacity = desiredOpacity;
        if (ElementComposition.GetElementVisual(element) is CompositionVisual cv)
        {
            cv.Offset = new Vector3D(0, 0, 0);
            cv.Scale = new Vector3D(1, 1, 1);
            cv.Opacity = (float)desiredOpacity;
        }
    }

    private Control FindSelectionIndicator(object item)
    {
        if (item == null)
            return null;

        var cont = NavigationViewItemOrSettingsContentFromData(item);
        if (cont == null)
            return null;

        var indicator = cont.SelectionIndicator;
        if (indicator != null)
            return indicator;

        cont.UpdateLayout();
        return cont.SelectionIndicator;
    }

    private FANavigationViewItem FindLowestLevelContainerToDisplaySelectionIndicator()
    {
        var indexIntoIndex = 1;
        var selIndex = _selectionModel.SelectedIndex;

        if (selIndex == IndexPath.Unselected || selIndex.GetSize() <= 1)
            return null;

        if (GetContainerForIndex(selIndex.GetAt(indexIntoIndex), selIndex.GetAt(0) == _footerMenuBlockIndex) is not FANavigationViewItem nvi)
            return null;

        var isRepVis = nvi.IsRepeaterVisible;

        while (nvi != null && isRepVis && !nvi.IsSelected && nvi.IsChildSelected)
        {
            indexIntoIndex++;
            isRepVis = false;

            if (nvi.GetRepeater != null)
            {
                if (nvi.GetRepeater.TryGetElement(selIndex.GetAt(indexIntoIndex)) is FANavigationViewItem childNVI)
                {
                    nvi = childNVI;
                    isRepVis = nvi.IsRepeaterVisible;
                }
                else
                {
                    nvi = null;
                }
            }
        }

        return nvi;
    }


    ////////////////////////////
    //////// OTHER ////////////
    //////////////////////////

    private void UpdateIsClosedCompact()
    {
        if (_splitView == null)
            return;

        var svdm = _splitView.DisplayMode;
        _isClosedCompact = !_splitView.IsPaneOpen &&
            (svdm == SplitViewDisplayMode.CompactInline || svdm == SplitViewDisplayMode.CompactOverlay);

        PseudoClasses.Set(s_pcClosedCompact, _isClosedCompact);
        PseudoClasses.Set(s_pcListSizeCompact, _isClosedCompact);

        UpdateTitleBarPadding();
        UpdateBackAndCloseButtonsVisibility();
        UpdatePaneToggleSize();
    }

    private void UpdatePaneTitleFrameworkElementParents()
    {
        if (_paneTitleHolderFrameworkElement == null)
            return;

        var isPaneTBVis = IsPaneToggleButtonVisible;
        var isTopNav = IsTopNavigationView;

        _isLeftPaneTitleEmpty = isPaneTBVis || isTopNav || string.IsNullOrEmpty(PaneTitle) ||
            (PaneDisplayMode == FANavigationViewPaneDisplayMode.LeftMinimal && !IsPaneOpen);

        _paneTitleHolderFrameworkElement.IsVisible = !_isLeftPaneTitleEmpty;

        if (_paneTitleFrameworkElement == null)
            return;

        var first = SetPaneTitleFrameworkElementParent(_paneToggleButton, _paneTitleFrameworkElement, isTopNav || !isPaneTBVis);
        var second = SetPaneTitleFrameworkElementParent(_paneTitlePresenter, _paneTitleFrameworkElement, isTopNav || isPaneTBVis);
        var third = SetPaneTitleFrameworkElementParent(_paneTitleOnTopPane, _paneTitleFrameworkElement, !isTopNav || isPaneTBVis);

        if (first != null)
        {
            first();
            _paneTitleOnTopPane.IsVisible = false;
        }
        else if (second != null)
        {
            second();
            _paneTitleOnTopPane.IsVisible = false;
        }
        else if (third != null)
        {
            third();
            if (_paneTitleOnTopPane != null)
                _paneTitleOnTopPane.IsVisible = !string.IsNullOrEmpty(PaneTitle) && PaneTitle.Length != 0;
        }
    }

    private static Action SetPaneTitleFrameworkElementParent(ContentControl parent, Control paneTitle, bool shouldNotContainPaneTitle)
    {
        if (parent == null)
            return null;

        if ((parent.Content == paneTitle) != shouldNotContainPaneTitle)
            return null;

        if (shouldNotContainPaneTitle)
        {
            parent.Content = null;
            return null;
        }

        return () => parent.Content = paneTitle;
    }

    internal void TopNavigationViewItemContentChanged()
    {
        if (!_appliedTemplate)
            return;

        if (MenuItemsSource == null) // WinUI #5558
            _topDataProvider.InvalidWidthCache();

        InvalidateMeasure();
    }

    private void CloseTopNavigationViewFlyout() => _topNavOverflowButton?.Flyout?.Hide();

    private void OnFlyoutClosing(object sender, CancelEventArgs args)
    {
        if (!_moveTopNavOverflowItemOnFlyoutClose || _selectionChangeFromOverflowMenu)
            return;

        _moveTopNavOverflowItemOnFlyoutClose = false;

        var selIndex = _selectionModel.SelectedIndex;
        if (selIndex.GetSize() <= 0)
            return;

        if (GetContainerForIndex(selIndex.GetAt(1), false) is FANavigationViewItem nvi)
            nvi.IsExpanded = false;

        SelectAndMoveOverflowItem(SelectedItem, selIndex, false);
    }

    private void UpdatePaneDisplayMode()
    {
        if (!_appliedTemplate)
            return;

        if (!IsTopNavigationView)
        {
            UpdateAdaptiveLayout(Bounds.Width, true);

            SwapPaneHeaderContent(_leftNavPaneHeaderContentBorder, _paneHeaderOnTopPane, PaneHeaderProperty);
            SwapPaneHeaderContent(_leftNavPaneCustomContentBorder, _paneCustomContentOnTopPane, PaneCustomContentProperty);
            SwapPaneHeaderContent(_leftNavFooterContentBorder, _paneFooterOnTopPane, PaneFooterProperty);
        }
        else
        {
            ClosePane();
            SetDisplayMode(FANavigationViewDisplayMode.Minimal, true);

            SwapPaneHeaderContent(_paneHeaderOnTopPane, _leftNavPaneHeaderContentBorder, PaneHeaderProperty);
            SwapPaneHeaderContent(_paneCustomContentOnTopPane, _leftNavPaneCustomContentBorder, PaneCustomContentProperty);
            SwapPaneHeaderContent(_paneFooterOnTopPane, _leftNavFooterContentBorder, PaneFooterProperty);
        }

        UpdateContentBindingsForPaneDisplayMode();
        UpdateRepeaterItemsSource(false);
        UpdateFooterRepeaterItemsSource(false, false);

        if (SelectedItem != null)
            _orientationChangedPendingAnimation = true;
    }

    private void UpdatePaneDisplayMode(FANavigationViewPaneDisplayMode oldMode, FANavigationViewPaneDisplayMode newMode)
    {
        if (!_appliedTemplate)
            return;

        UpdatePaneDisplayMode();

        if (IsTopNavigationView || newMode != PaneDisplayMode)
            return;

        if (IsPaneOpen)
        {
            if (newMode == FANavigationViewPaneDisplayMode.LeftMinimal)
                ClosePane();
        }
        else if (oldMode == FANavigationViewPaneDisplayMode.LeftMinimal &&
                 newMode == FANavigationViewPaneDisplayMode.Left)
        {
            OpenPane();
        }
    }

    private void UpdatePaneVisibility()
    {
        if (IsPaneVisible)
        {
            var topNav = IsTopNavigationView;
            TemplateSettings.LeftPaneVisibility = !topNav;
            TemplateSettings.TopPaneVisibility = topNav;
            PseudoClasses.Set(s_pcPaneCollapsed, false);
        }
        else
        {
            TemplateSettings.LeftPaneVisibility = false;
            TemplateSettings.TopPaneVisibility = false;
            PseudoClasses.Set(s_pcPaneCollapsed, true);
        }
    }

    private void SwapPaneHeaderContent(ContentControl newParent, ContentControl oldParent, AvaloniaProperty targetProperty)
    {
        if (newParent == null)
            return;

        oldParent?.SetValue(ContentControl.ContentProperty, null);
        newParent[!ContentControl.ContentProperty] = this[!targetProperty];
    }

    private void UpdateContentBindingsForPaneDisplayMode()
    {
        var asb = IsTopNavigationView ? _topNavAutoSuggestBoxPresenter : _leftNavAutoSuggestBoxPresenter;
        var not = IsTopNavigationView ? _leftNavAutoSuggestBoxPresenter : _topNavAutoSuggestBoxPresenter;

        if (asb == null)
            return;

        not?.SetValue(ContentControl.ContentProperty, null);
        asb[!ContentControl.ContentProperty] = this[!AutoCompleteBoxProperty];
    }

    private void UpdateHeaderVisibility()
    {
        if (_appliedTemplate)
            UpdateHeaderVisibility(DisplayMode);
    }

    private void UpdateHeaderVisibility(FANavigationViewDisplayMode dMode)
    {
        var showHeader = Header != null &&
            (AlwaysShowHeader || (!IsTopNavigationView && dMode == FANavigationViewDisplayMode.Minimal));

        PseudoClasses.Set(s_pcHeaderCollapsed, !showHeader);
    }

    private void UpdateTitleBarPadding()
    {
        if (!_appliedTemplate)
            return;

        var setPaneTitleHolderFEMargin = _paneTitleHolderFrameworkElement?.IsVisible == true;
        var setPaneToggleButtonMargin = !setPaneTitleHolderFEMargin && _paneToggleButton?.IsVisible == true;

        if (!setPaneTitleHolderFEMargin && !setPaneToggleButtonMargin)
            return;

        var thickness = new Thickness();

        if (ShouldShowBackButton)
        {
            thickness = IsOverlay
                ? new Thickness(_backButtonWidth, 0, 0, 0)
                : new Thickness(0, _backButtonHeight, 0, 0);
        }
        else if (ShouldShowCloseButton && IsOverlay)
        {
            thickness = new Thickness(_backButtonWidth, 0, 0, 0);
        }

        if (setPaneTitleHolderFEMargin)
            _paneTitleHolderFrameworkElement.Margin = thickness;
        else
            _paneToggleButton.Margin = thickness;
    }

    private void UpdatePaneShadow() { }

    private T GetContainerForData<T>(object data) where T : Control
    {
        if (data == null)
            return default;

        if (data is T t)
            return t;

        var mainRepeater = IsTopNavigationView ? _topNavRepeater : _leftNavRepeater;
        var itemIndex = GetIndexFromItem(mainRepeater, data);
        if (itemIndex >= 0 && mainRepeater.TryGetElement(itemIndex) is T mainCont)
            return mainCont;

        var footerRepeater = IsTopNavigationView ? _topNavFooterMenuRepeater : _leftNavFooterMenuRepeater;
        itemIndex = GetIndexFromItem(footerRepeater, data);
        if (itemIndex >= 0 && footerRepeater.TryGetElement(itemIndex) is T footerCont)
            return footerCont;

        return SearchEntireTreeForContainer(mainRepeater, data) as T
            ?? SearchEntireTreeForContainer(footerRepeater, data) as T;
    }

    private Control SearchEntireTreeForContainer(FAItemsRepeater ir, object data)
    {
        var index = GetIndexFromItem(ir, data);
        if (index != -1)
            return ir.TryGetElement(index);

        for (var i = 0; i < GetContainerCountInRepeater(ir); i++)
        {
            if (ir.TryGetElement(i) is FANavigationViewItem nvi && nvi.GetRepeater != null)
            {
                var foundElement = SearchEntireTreeForContainer(nvi.GetRepeater, data);
                if (foundElement != null)
                    return foundElement;
            }
        }

        return null;
    }

    private IndexPath SearchEntireTreeForIndexPath(FAItemsRepeater ir, object data, bool isFooterRepeater)
    {
        for (var i = 0; i < GetContainerCountInRepeater(ir); i++)
        {
            if (ir.TryGetElement(i) is FANavigationViewItem nvi)
            {
                var ip = new IndexPath(new[] { isFooterRepeater ? _footerMenuBlockIndex : _mainMenuBlockIndex, i });
                var indexPath = SearchEntireTreeForIndexPath(nvi, data, ip);
                if (indexPath != IndexPath.Unselected)
                    return indexPath;
            }
        }

        return IndexPath.Unselected;
    }

    private IndexPath SearchEntireTreeForIndexPath(FANavigationViewItem nviParent, object data, IndexPath ip)
    {
        var areChildrenRealized = false;
        var childRepeater = nviParent.GetRepeater;

        if (childRepeater != null && DoesRepeaterHaveRealizedContainers(childRepeater))
        {
            areChildrenRealized = true;
            for (var i = 0; i < GetContainerCountInRepeater(childRepeater); i++)
            {
                if (childRepeater.TryGetElement(i) is FANavigationViewItem nvi)
                {
                    var newIP = ip.CloneWithChildIndex(i);
                    if (nvi.Content == data)
                        return newIP;

                    var foundIP = SearchEntireTreeForIndexPath(nvi, data, newIP);
                    if (foundIP != IndexPath.Unselected)
                        return foundIP;
                }
                else
                {
                    areChildrenRealized = false;
                }
            }
        }

        if (!areChildrenRealized)
        {
            var childrenData = GetChildren(nviParent);
            if (childrenData != null)
            {
                for (var i = 0; i < childrenData.Count(); i++)
                {
                    var newIP = ip.CloneWithChildIndex(i);
                    if (childrenData.ElementAt(i) == data)
                        return newIP;
                }
            }
        }

        return IndexPath.Unselected;
    }

    private FANavigationViewItemBase ResolveContainerForItem(object item, int index)
    {
        var args = new FAElementFactoryGetArgs { Data = item, Index = index };
        return _itemsFactory.GetElement(args) as FANavigationViewItemBase;
    }

    private void RecycleContainer(Control container)
        => _itemsFactory.RecycleElement(new FAElementFactoryRecycleArgs { Element = container });

    private Control GetContainerForIndex(int index, bool inFooter)
    {
        if (IsTopNavigationView)
        {
            var ir = inFooter
                ? _topNavFooterMenuRepeater
                : (_topDataProvider.IsItemInPrimaryList(index) ? _topNavRepeater : _topNavRepeaterOverflowView);

            var irIndex = inFooter ? index : _topDataProvider.ConvertOriginalIndexToIndex(index);
            return ir.TryGetElement(irIndex);
        }

        return inFooter
            ? _leftNavFooterMenuRepeater.TryGetElement(index)
            : _leftNavRepeater.TryGetElement(index);
    }

    private FANavigationViewItemBase GetContainerForIndexPath(IndexPath ip, bool lastVisible = false, bool forceRealize = false)
    {
        if (ip == IndexPath.Unselected || ip.GetSize() <= 0)
            return null;

        var cont = GetContainerForIndex(ip.GetAt(1), ip.GetAt(0) == _footerMenuBlockIndex);
        if (cont == null)
            return null;

        if (lastVisible && cont is FANavigationViewItem { IsExpanded: false } nvi)
            return nvi;

        return GetContainerForIndexPath(cont, ip, lastVisible, forceRealize);
    }

    private FANavigationViewItemBase GetContainerForIndexPath(Control first, IndexPath ip, bool lastVisible, bool forceRealize)
    {
        var cont = first;
        if (ip.GetSize() > 2)
        {
            for (var i = 2; i < ip.GetSize(); i++)
            {
                if (cont is not FANavigationViewItem nvi)
                    return null;

                if (lastVisible && !nvi.IsExpanded)
                    return nvi;

                var nviRepeater = nvi.GetRepeater;
                if (nviRepeater == null)
                    return null;

                var index = ip.GetAt(i);
                var nextCont = forceRealize
                    ? nviRepeater.GetOrCreateElement(index)
                    : nviRepeater.TryGetElement(index);

                if (nextCont == null)
                    return null;

                cont = nextCont;
            }
        }

        return cont as FANavigationViewItemBase;
    }

    private IEnumerable GetChildrenForItemInIndexPath(IndexPath ip, bool forceRealize)
    {
        if (ip == IndexPath.Unselected || ip.GetSize() <= 1)
            return null;

        var cont = GetContainerForIndex(ip.GetAt(1), ip.GetAt(0) == _footerMenuBlockIndex);
        return cont == null ? null : GetChildrenForItemInIndexPath(cont, ip, forceRealize);
    }

    private IEnumerable GetChildrenForItemInIndexPath(Control first, IndexPath ip, bool forceRealize)
    {
        var container = first;
        var shouldRecycle = false;

        if (ip.GetSize() > 2)
        {
            for (var i = 2; i < ip.GetSize(); i++)
            {
                if (container is not FANavigationViewItem nvi)
                    return null;

                var nextContIndex = ip.GetAt(i);
                var nviRep = nvi.GetRepeater;
                var succeed = false;

                if (nviRep != null && DoesRepeaterHaveRealizedContainers(nviRep))
                {
                    var nextCont = nviRep.TryGetElement(nextContIndex);
                    if (nextCont != null)
                    {
                        container = nextCont;
                        succeed = true;
                    }
                }
                else if (forceRealize)
                {
                    var childrenData = GetChildren(nvi);
                    if (childrenData != null)
                    {
                        if (shouldRecycle)
                        {
                            RecycleContainer(nvi);
                            shouldRecycle = false;
                        }

                        var data = childrenData.ElementAt(nextContIndex);
                        if (data != null && ResolveContainerForItem(data, nextContIndex) is FANavigationViewItem nextNVI)
                        {
                            container = nextNVI;
                            shouldRecycle = true;
                            succeed = true;
                        }
                    }
                }

                if (!succeed)
                    return null;
            }
        }

        if (container is not FANavigationViewItem finalNVI)
            return null;

        var children = GetChildren(finalNVI);
        if (shouldRecycle)
            RecycleContainer(finalNVI);

        return children;
    }

    private void UpdateOpenPaneWidth(double width)
    {
        if (IsTopNavigationView || _splitView == null)
            return;

        _openPaneWidth = Math.Max(0, Math.Min(width, OpenPaneLength));
        TemplateSettings.OpenPaneWidth = _openPaneWidth;
    }

    private void SetPaneToggleButtonAutomationName()
    {
        // Dynamic - changes based on IsPaneOpen state; cannot be static in XAML.
        var name = IsPaneOpen ? "Close Navigation" : "Open Navigation";
        if (_paneToggleButton != null)
            ToolTip.SetTip(_paneToggleButton, name);
    }

    private class StepEasingFunction : Easing
    {
        public int Steps { get; set; }
        public override double Ease(double progress)
            => Math.Round(progress * Steps) * (1 / Steps);
    }
}
