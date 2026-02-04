using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using AetherBags.Inventory.State;
using AetherBags.Monitoring;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.ContextMenu;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract unsafe class InventoryAddonBase : NativeAddon, IInventoryWindow
{
    protected readonly InventoryCategoryHoverCoordinator HoverCoordinator = new();
    protected readonly InventoryCategoryPinCoordinator PinCoordinator = new();
    protected readonly HashSet<InventoryCategoryNode> HoverSubscribed = new();

    protected DragDropNode BackgroundDropTarget = null!;
    protected ScrollingAreaNode<WrappingGridNode<InventoryCategoryNodeBase>> ScrollableCategories = null!;
    protected WrappingGridNode<InventoryCategoryNodeBase> CategoriesNode = null!;
    protected TextInputWithButtonNode SearchInputNode = null!;
    protected InventoryFooterNode FooterNode = null!;
    protected TextNode? SlotCounterNode { get; set; }
    protected CircleButtonNode SettingsButtonNode = null!;

    internal ContextMenu ContextMenu = null!;

    protected readonly SharedNodePool<InventoryDragDropNode> SharedItemNodePool = new(
        maxSize: 256,
        factory: null,
        resetAction: node => node.ResetForReuse());

    protected readonly SharedNodePool<InventoryCategoryNode> SharedCategoryNodePool = new(
        maxSize: 32,
        factory: null,
        resetAction: node => node.ResetForReuse());

    protected readonly VirtualizationState CategoryVirtualization = new() { BufferSize = 200f };

    protected virtual float MinWindowWidth => 600;
    protected virtual float MaxWindowWidth => 800;
    protected virtual float MinWindowHeight => 200;
    protected virtual float MaxWindowHeight => 1000;

    protected const float CategorySpacing = 12;
    protected const float ItemSize = 42;
    protected const float ItemPadding = 5;
    protected const float FooterHeight = 28f;
    protected const float FooterTopSpacing = 4f;
    protected const float SettingsButtonOffset = 62f;
    protected const float ScrollBarWidth = 16f;
    protected const float ContentHeightOffset = 4f;

    protected bool RefreshQueued;
    protected bool RefreshAutosizeQueued;
    protected bool IsSetupComplete;
    private bool _deferredPopulationInProgress;
    private bool _initialPopulationComplete;
    private const int ItemsPerFrame = 50;

    protected abstract InventoryStateBase InventoryState { get; }

    protected virtual bool HasFooter => true;
    protected virtual bool HasPinning => true;
    protected virtual bool HasSlotCounter => false;

    private readonly HashSet<uint> _searchMatchScratch = new();
    private bool _isRefreshing;
    private string _lastSearchText = string.Empty;

    private int _requestedUpdateCount;
    private int _refreshFromLifecycleCount;
    private long _lastLogTick;

    public void ManualRefresh() => ExecuteRefresh(true);

    public string GetSearchText() => SearchInputNode?.SearchString.ExtractText() ?? string.Empty;

    public InventoryStats GetStats() => InventoryState.GetStats();

    public IReadOnlyList<CategorizedInventory>? GetVisibleCategories()
    {
        if (!IsSetupComplete) return null;
        string filter = GetSearchText();
        return InventoryState.GetCategories(filter);
    }

    public virtual void SetSearchText(string searchText)
    {
        Services.Framework.RunOnTick(() =>
        {
            if (IsOpen) SearchInputNode.SearchString = searchText;
            RefreshCategoriesCore(autosize: true);
        }, delayTicks: 3);
    }

    private void ExecuteRefresh(bool autosize)
    {
        if (!IsSetupComplete || !IsOpen || _isRefreshing) return;

        try
        {
            _isRefreshing = true;
            InventoryState.RefreshFromGame();
            System.LootedItemsTracker.FlushPendingChanges();
            RefreshCategoriesCore(autosize);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void RefreshFromLifecycle() => ExecuteRefresh(autosize: true);

    protected virtual void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        var config = System.Config.General;
        string searchText = SearchInputNode.SearchString.ExtractText();
        bool isSearching = !string.IsNullOrWhiteSpace(searchText);

        if (searchText != _lastSearchText)
        {
            _lastSearchText = searchText;
            System.AetherBagsAPI?.API.RaiseSearchChanged(searchText);
        }

        if (config.SearchMode == SearchMode.Highlight && isSearching)
        {
            _searchMatchScratch.Clear();
            var allData = InventoryState.GetCategories(string.Empty);

            for (int i = 0; i < allData.Count; i++)
            {
                var cat = allData[i];
                for (int j = 0; j < cat.Items.Count; j++)
                {
                    var item = cat.Items[j];
                    if (item.IsRegexMatch(searchText))
                    {
                        _searchMatchScratch.Add(item.Item.ItemId);
                    }
                }
            }
            HighlightState.SetFilter(HighlightSource.Search, _searchMatchScratch);
        }
        else
        {
            HighlightState.ClearFilter(HighlightSource.Search);
        }

        if (SearchInputNode != null)
        {
            bool atActive = !string.IsNullOrEmpty(HighlightState.SelectedAllaganToolsFilterKey);

            SearchInputNode.HintAddColor = (atActive)
                ? new Vector3(0.0f, 0.3f, 0.3f)
                : Vector3.Zero;
        }

        if (HasFooter)
        {
            FooterNode.SlotAmountText = InventoryState.GetEmptySlotsString();
            FooterNode.RefreshCurrencies();
        }

        string dataFilter = config.SearchMode == SearchMode.Filter ? searchText : string.Empty;
        var categories = InventoryState.GetCategories(dataFilter);

        float maxContentWidth = CategoriesNode.Width > 0 ? CategoriesNode.Width : ContentSize.X;
        int maxItemsPerLine = CalculateOptimalItemsPerLine(maxContentWidth);

        bool deferItems = !_deferredPopulationInProgress && !_initialPopulationComplete;

        CategoriesNode.SyncWithListDataByKey<CategorizedInventory, InventoryCategoryNode, uint>(
            dataList: categories,
            getKeyFromData: categorizedInventory => categorizedInventory.Key,
            getKeyFromNode: node => node.CategorizedInventory.Key,
            updateNode: (node, data) =>
            {
                node.SetCategoryData(data, Math.Min(data.Items.Count, maxItemsPerLine), deferItemCreation: deferItems);
                if (!deferItems) node.RefreshNodeVisuals();
            },
            createNodeMethod: _ => CreateCategoryNode(),
            resetNodeForReuse: ResetCategoryNodeForReuse,
            externalPool: SharedCategoryNodePool);

        if (HasPinning)
        {
            bool pinsChanged = PinCoordinator.ApplyPinnedStates(CategoriesNode);
            if (pinsChanged) HoverCoordinator.ResetAll(CategoriesNode);
        }

        WireHoverHandlers();

        CategoriesNode.InvalidateLayout();

        if (autosize)
            AutoSizeWindow();
        else
        {
            LayoutContent();
            CategoriesNode.RecalculateLayout();
        }

        if (deferItems && !_deferredPopulationInProgress)
        {
            StartDeferredItemPopulation();
        }
        else if (!deferItems && !_initialPopulationComplete)
        {
            _initialPopulationComplete = true;
        }

        System.AetherBagsAPI?.API.RaiseCategoriesRefreshed();
    }

    private void StartDeferredItemPopulation()
    {
        _deferredPopulationInProgress = true;
        Services.Framework.RunOnTick(PopulateCategoryBatch, delayTicks: 1);
    }

    private void PopulateCategoryBatch()
    {
        if (!IsOpen)
        {
            _deferredPopulationInProgress = false;
            return;
        }

        UpdateCategoryVisibility();

        int itemsPopulated = 0;
        using (CategoriesNode.DeferRecalculateLayout())
        {
            var nodes = CategoriesNode.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is not InventoryCategoryNode categoryNode || !categoryNode.NeedsItemPopulation)
                    continue;

                if (!CategoryVirtualization.IsVisible(i))
                    continue;

                int categoryItemCount = categoryNode.CategorizedInventory.Items.Count;

                if (itemsPopulated > 0 && itemsPopulated + categoryItemCount > ItemsPerFrame)
                    break;

                categoryNode.PopulateItems();
                categoryNode.RefreshNodeVisuals();
                itemsPopulated += categoryItemCount;

                if (itemsPopulated >= ItemsPerFrame)
                    break;
            }

            if (itemsPopulated < ItemsPerFrame)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] is not InventoryCategoryNode categoryNode || !categoryNode.NeedsItemPopulation)
                        continue;

                    if (CategoryVirtualization.IsVisible(i))
                        continue;

                    int categoryItemCount = categoryNode.CategorizedInventory.Items.Count;

                    if (itemsPopulated > 0 && itemsPopulated + categoryItemCount > ItemsPerFrame)
                        break;

                    categoryNode.PopulateItems();
                    categoryNode.RefreshNodeVisuals();
                    itemsPopulated += categoryItemCount;

                    if (itemsPopulated >= ItemsPerFrame)
                        break;
                }
            }
        }

        bool hasMore = false;
        foreach (var node in CategoriesNode.Nodes)
        {
            if (node is InventoryCategoryNode categoryNode && categoryNode.NeedsItemPopulation)
            {
                hasMore = true;
                break;
            }
        }

        if (hasMore)
        {
            Services.Framework.RunOnTick(PopulateCategoryBatch);
        }
        else
        {
            _deferredPopulationInProgress = false;
            _initialPopulationComplete = true;
        }
    }

    protected readonly struct HeaderLayout
    {
        public Vector2 SearchPosition { get; init; }
        public Vector2 SearchSize { get; init; }
        public float HeaderWidth { get; init; }
        public float HeaderY { get; init; }
    }

    protected HeaderLayout CalculateHeaderLayout(AtkUnitBase* addon)
    {
        var header = addon->WindowHeaderCollisionNode;
        float headerW = header->Width;

        float itemY = header->Y + (header->Height - 28f) * 0.5f;

        // Reserve space for close button (~50px) and settings button (~48px + gap)
        const float closeButtonReserve = 50f;
        const float settingsButtonWidth = 28f;
        const float minGap = 16f;
        const float minSearchWidth = 150f;
        const float maxSearchWidth = 350f;

        // Calculate max available width for search bar
        // Layout from right: [closeButton 50px] [settings 28px] [gap 16px] [searchBar] [gap 16px] [leftContent]
        float rightReserve = closeButtonReserve + settingsButtonWidth + minGap;
        float leftReserve = 220f; // Space for title (e.g. "Chocobo Saddlebag" is ~200px)
        float availableForSearch = headerW - rightReserve - leftReserve;

        // Search bar width: prefer 45% of header, but clamp to available space and min/max
        float desiredSearchWidth = headerW * 0.45f;
        float searchWidth = Math.Clamp(desiredSearchWidth, minSearchWidth, Math.Min(maxSearchWidth, availableForSearch));

        // Center the search bar, but ensure it doesn't extend past the safe right boundary
        float maxSearchRight = headerW - rightReserve;
        float centeredSearchX = (headerW - searchWidth) * 0.5f;
        float searchRight = centeredSearchX + searchWidth;

        // If centered position would overlap with right elements, shift left
        float searchX = searchRight > maxSearchRight
            ? maxSearchRight - searchWidth
            : centeredSearchX;

        // Ensure search bar doesn't go past left reserve
        if (searchX < leftReserve)
            searchX = leftReserve;

        return new HeaderLayout
        {
            SearchPosition = new Vector2(searchX, itemY),
            SearchSize = new Vector2(searchWidth, 28f),
            HeaderWidth = headerW,
            HeaderY = itemY
        };
    }

    protected void InitializeBackgroundDropTarget()
    {
        BackgroundDropTarget = new DragDropNode
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            IconId = 0,
            IsDraggable = false,
            IsClickable = false,
            AcceptedType = DragDropType.Item,
        };

        BackgroundDropTarget.DragDropBackgroundNode.IsVisible = false;
        BackgroundDropTarget.IconNode.IsVisible = false;

        BackgroundDropTarget.OnPayloadAccepted = OnBackgroundPayloadAccepted;

        BackgroundDropTarget.AttachNode(this);
    }

    protected virtual InventoryCategoryNode CreateCategoryNode()
    {
        var node = SharedCategoryNodePool.TryRent();
        if (node == null)
        {
            node = new InventoryCategoryNode
            {
                Size = ContentSize with { Y = 120 },
                SharedItemPool = SharedItemNodePool,
            };
        }

        node.OnRefreshRequested = ManualRefresh;
        node.OnDragEnd = () => InventoryOrchestrator.RefreshAll(updateMaps: true);
        node.SharedItemPool = SharedItemNodePool;
        return node;
    }

    private static void ResetCategoryNodeForReuse(InventoryCategoryNode node)
    {
        node.ResetForReuse();
    }

    private void OnBackgroundPayloadAccepted(DragDropNode node, DragDropPayload acceptedPayload)
    {
        if (!acceptedPayload.IsValidInventoryPayload) return;

        InventoryLocation emptyLocation = InventoryScanner.GetFirstEmptySlot(InventoryState.SourceType);

        if (!emptyLocation.IsValid)
        {
            Services.Logger.Error("No empty slots available to receive drop.");
            return;
        }

        InventoryMappedLocation visualLocation = InventoryContextState.GetVisualLocation(emptyLocation.Container, emptyLocation.Slot);

        var visualInvType = InventoryType.GetInventoryTypeFromContainerId(visualLocation.Container);
        int absoluteIndex = visualInvType.GetInventoryStartIndex + visualLocation.Slot;

        var targetPayload = new DragDropPayload
        {
            Type = DragDropType.Item,
            Int1 = visualLocation.Container,
            Int2 = visualLocation.Slot,
            ReferenceIndex = (short)absoluteIndex
        };

        Services.Logger.DebugOnly($"[BackgroundDrop] Target: {emptyLocation} -> Visual: {visualLocation} (Ref: {absoluteIndex})");

        InventoryMoveHelper.HandleItemMovePayload(acceptedPayload, targetPayload);

        ManualRefresh();
    }

    protected void WireHoverHandlers()
    {
        var nodes = CategoriesNode.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryCategoryNode node)
                continue;

            if (!HoverSubscribed.Add(node))
                continue;

            node.HeaderHoverChanged += (src, hovering) =>
            {
                HoverCoordinator.OnCategoryHoverChanged(CategoriesNode, src, hovering);
            };
        }
    }

    protected int CalculateOptimalItemsPerLine(float availableWidth)
        => Math.Clamp((int)MathF.Floor((availableWidth + ItemPadding) / (ItemSize + ItemPadding)), 1, 15);

    protected virtual void LayoutContent()
    {
        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerH = HasFooter || HasSlotCounter ?  FooterHeight : 0;

        if (HasFooter)
        {
            FooterNode.Position = new Vector2(contentPos.X, contentPos.Y + contentSize.Y - footerH);
            FooterNode.Size = new Vector2(contentSize.X, footerH);
        }
        else if (HasSlotCounter && SlotCounterNode != null)
        {
            SlotCounterNode.Position = new Vector2(contentSize.X -80f, contentPos.Y + contentSize.Y - footerH + 4f);
        }

        float gridH = contentSize.Y - (HasFooter ? FooterHeight + FooterTopSpacing : 0);
        if (gridH < 0) gridH = 0;

        ScrollableCategories.Position = contentPos;
        ScrollableCategories.Size = new Vector2(contentSize.X, gridH);

        float categoriesWidth = contentSize.X - ScrollBarWidth;
        CategoriesNode.Width = categoriesWidth;

        UpdateCategoryMaxWidths(categoriesWidth);
    }

    private void UpdateCategoryMaxWidths(float maxWidth)
    {
        foreach (var node in CategoriesNode.Nodes)
        {
            if (node is InventoryCategoryNodeBase categoryNode && categoryNode.MaxWidth != maxWidth)
            {
                categoryNode.MaxWidth = maxWidth;
                categoryNode.RecalculateSize();
            }
        }
    }

    protected virtual void AutoSizeWindow()
    {
        var nodes = CategoriesNode.Nodes;

        float maxChildWidth = 0f;
        int childCount = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryCategoryNodeBase cat)
                continue;

            childCount++;
            float w = cat.Width;
            if (w > maxChildWidth) maxChildWidth = w;
        }

        if (childCount == 0)
        {
            ResizeWindow(MinWindowWidth, MinWindowHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

        float footerSpace = HasFooter || HasSlotCounter ? FooterHeight + FooterTopSpacing : 0;

        float requiredWidth = maxChildWidth + ScrollBarWidth + (ContentStartPosition.X * 2);
        float finalWidth = Math.Clamp(requiredWidth, MinWindowWidth, MaxWindowWidth);

        if (SettingsButtonNode != null)
        {
            SettingsButtonNode.X = finalWidth - SettingsButtonOffset;
        }

        float contentWidth = finalWidth - (ContentStartPosition.X * 2);
        float categoriesWidth = contentWidth - ScrollBarWidth;

        CategoriesNode.Width = categoriesWidth;
        UpdateCategoryMaxWidths(categoriesWidth);
        CategoriesNode.RecalculateLayout();

        float requiredGridHeight = CategoriesNode.GetRequiredHeight();

        float requiredContentHeight = requiredGridHeight + footerSpace;
        float requiredWindowHeight = requiredContentHeight + ContentStartPosition.Y + ContentStartPosition.X + ContentHeightOffset;
        float finalHeight = Math.Clamp(requiredWindowHeight, MinWindowHeight, MaxWindowHeight);

        ResizeWindow(finalWidth, finalHeight, recalcLayout: false);

        UpdateScrollParameters();
    }

    protected void UpdateScrollParameters()
    {
        if (ScrollableCategories == null) return;

        float requiredHeight = CategoriesNode.GetRequiredHeight();
        ScrollableCategories.ContentHeight = requiredHeight;

        CategoryVirtualization.ViewportHeight = ScrollableCategories.Size.Y;
        UpdateCategoryVisibility();
    }

    private void OnScrollValueChanged(int scrollPosition)
    {
        CategoryVirtualization.ScrollPosition = scrollPosition;
    }

    private void UpdateCategoryVisibility()
    {
        var nodes = CategoriesNode.Nodes;
        CategoryVirtualization.SetItemCount(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is InventoryCategoryNodeBase cat)
            {
                CategoryVirtualization.SetItemLayout(i, cat.Y, cat.Height);
            }
        }

        CategoryVirtualization.UpdateVisibility();
    }

    protected void ResizeWindow(float width, float height, bool recalcLayout)
    {
        SetWindowSize(width, height);

        if (BackgroundDropTarget != null)
        {
            BackgroundDropTarget.Size = ContentSize;
        }

        UpdateHeaderLayout();
        LayoutContent();

        if (recalcLayout)
            CategoriesNode.RecalculateLayout();

        UpdateScrollParameters();
    }

    protected virtual void UpdateHeaderLayout()
    {
        AtkUnitBase* addon = this;
        if (addon == null) return;

        var header = CalculateHeaderLayout(addon);

        if (SearchInputNode != null)
        {
            SearchInputNode.Position = header.SearchPosition;
            SearchInputNode.Size = header.SearchSize;
        }

        if (SettingsButtonNode != null)
        {
            SettingsButtonNode.Position = new Vector2(header.HeaderWidth - SettingsButtonOffset, header.HeaderY);
        }
    }

    protected void ResizeWindow(float width, float height)
        => ResizeWindow(width, height, recalcLayout: true);

    public void ItemRefresh()
    {
        if (!IsOpen) return;
        if (!IsSetupComplete) return;

        RefreshCategoriesCore(false);
    }

    private void LogRefreshStats()
    {
        long now = Environment.TickCount64;
        if (now - _lastLogTick > 1000) // Log every second
        {
            Services.Logger.DebugOnly($"[Perf] Last 1s: OnRequestedUpdate={_requestedUpdateCount}, RefreshFromLifecycle={_refreshFromLifecycleCount}");
            _requestedUpdateCount = 0;
            _refreshFromLifecycleCount = 0;
            _lastLogTick = now;
        }
    }


    protected override void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        if (DragDropState.IsDragging) return;
        ExecuteRefresh(autosize: true);
    }


    protected override void OnSetup(AtkUnitBase* addon)
    {
        ContextMenu = new ContextMenu();

        System.AetherBagsAPI?.API.RaiseInventoryOpened();

        if (ScrollableCategories != null)
        {
            ScrollableCategories.ScrollBarNode.OnValueChanged = OnScrollValueChanged;
        }

        base.OnSetup(addon);
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (RefreshQueued)
        {
            bool doAutosize = RefreshAutosizeQueued;
            RefreshQueued = false;
            RefreshAutosizeQueued = false;

            RefreshCategoriesCore(doAutosize);
        }

        base.OnUpdate(addon);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        System.AetherBagsAPI?.API.RaiseInventoryClosed();

        ContextMenu?.Dispose();
        HoverSubscribed.Clear();
        RefreshQueued = false;
        RefreshAutosizeQueued = false;
        _deferredPopulationInProgress = false;
        _initialPopulationComplete = false;

        SharedItemNodePool.Clear();
        SharedCategoryNodePool.Clear();
        CategoryVirtualization.ClearLayout();

        base.OnFinalize(addon);
    }
}