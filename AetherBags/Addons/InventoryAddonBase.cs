using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Hooks;
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
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.ContextMenu;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract class InventoryAddonBase : NativeAddon, IInventoryWindow
{
    protected readonly InventoryCategoryHoverCoordinator HoverCoordinator = new();
    protected readonly InventoryCategoryPinCoordinator PinCoordinator = new();
    protected readonly HashSet<InventoryCategoryNode> HoverSubscribed = new();

    protected DragDropNode BackgroundDropTarget = null!;
    protected InventoryScrollingAreaNode<WrappingGridNode<InventoryCategoryNodeBase>> ScrollableCategories = null!;
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

    protected readonly Debouncer SearchDebouncer = new(System.Config.General.SearchDelay);

    protected virtual InventoryWindowSizingLimits WindowSizingLimits => InventoryWindowSizingLimits.Inventory;
    protected virtual float MinWindowWidth => WindowSizingLimits.SafeMinWidth;
    protected virtual float MaxWindowWidth => WindowSizingLimits.DefaultMaxWidth;
    protected virtual float MinWindowHeight => WindowSizingLimits.SafeMinHeight;
    protected virtual float MaxWindowHeight => WindowSizingLimits.DefaultMaxHeight;
    protected virtual InventoryWindowSizingSettings WindowSizingSettings => System.Config.General.InventoryWindowSizing;

    protected const float CategorySpacing = 12;
    protected const float ItemSize = 44;
    protected const float ItemPadding = 2;
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
    private const int ItemsPerFrame = 40;

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

    public unsafe void FocusSearch()
    {
        Services.Framework.RunOnTick(() =>
        {
            AtkUnitBase* addon = this;
            if (!IsOpen || SearchInputNode == null || SearchInputNode.FocusNode == null || addon == null) return;

            SearchInputNode.SetInputFocus();
        }, delayTicks: 2);
    }

    public unsafe bool TryFocusSearch()
    {
        AtkUnitBase* addon = this;
        if (!IsOpen || SearchInputNode == null || SearchInputNode.FocusNode == null || addon == null) return false;
        if (!IsFocusedAddon(addon)) return false;

        SearchInputNode.SetInputFocus();
        return true;
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

        bool deferItems = config.FrameBatchingEnabled && !_deferredPopulationInProgress && !_initialPopulationComplete;

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

    protected unsafe HeaderLayout CalculateHeaderLayout()
    {
        var addon = InternalAddon;

        float headerW;
        float headerY;
        float headerH;

        if (addon != null && addon->WindowHeaderCollisionNode != null)
        {
            var header = addon->WindowHeaderCollisionNode;
            headerW = header->Width;
            headerY = header->Y;
            headerH = header->Height;
        }
        else if (WindowNode is { } windowNode)
        {
            var headerNode = ((WindowNode)windowNode).HeaderContainerNode;
            headerW = headerNode.Width > 0 ? headerNode.Width : Size.X;
            headerY = headerNode.Y;
            headerH = headerNode.Height > 0 ? headerNode.Height : 38f;
        }
        else
        {
            headerW = Size.X;
            headerY = 0f;
            headerH = 38f;
        }

        const float searchHeight = 28f;
        float itemY = headerY + (headerH - searchHeight) * 0.5f;

        // Space for title (e.g. "AetherRetainerbag" is ~150px)
        const float titleReserve = 160f;
        const float gap = 8f;
        float leftReserve = titleReserve + gap;
        float rightReserve = SettingsButtonOffset + gap;

        const float minSearchWidth = 200f;
        const float maxSearchWidth = 360f;

        float available = headerW - rightReserve - leftReserve;
        float searchWidth = Math.Clamp(
            headerW * 0.45f,
            minSearchWidth,
            Math.Max(minSearchWidth, Math.Min(maxSearchWidth, available)));

        float centeredX = (headerW - searchWidth) * 0.5f;
        float minX = leftReserve;
        float maxX = headerW - rightReserve - searchWidth;
        float searchX = Math.Clamp(centeredX, minX, Math.Max(minX, maxX));

        return new HeaderLayout
        {
            SearchPosition = new Vector2(searchX, itemY),
            SearchSize = new Vector2(searchWidth, searchHeight),
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
            // Everything = catch-all so Crystal / EventItem payloads from our other windows
            // (or vanilla) reach OnBackgroundPayloadAccepted; the handler branches per payload
            // type and rejects anything we don't know how to route.
            AcceptedType = DragDropType.Everything,
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

        Services.Logger.Information($"[BackgroundDrop] enter: payloadType={acceptedPayload.Type}, payload=({acceptedPayload.Int1}@{acceptedPayload.Int2}), windowSource={InventoryState.SourceType}");

        var mapped = InventoryType.GetInventoryTypeFromContainerId(acceptedPayload.Int1);
        InventoryType srcContainer = mapped != 0 ? mapped : (InventoryType)acceptedPayload.Int1;
        ushort srcSlot = (ushort)acceptedPayload.Int2;

        if (acceptedPayload.Type == DragDropType.Crystal)
        {
            if (InventoryState.SourceType == InventorySourceType.Retainer
                && srcContainer == InventoryType.Crystals)
            {
                if (RetainerCommands.TryEntrust(srcContainer, srcSlot))
                {
                    ManualRefresh();
                    return;
                }
            }
            else if (InventoryState.SourceType == InventorySourceType.MainBags
                     && srcContainer == InventoryType.RetainerCrystals)
            {
                if (RetainerCommands.TryRetrieve(srcContainer, srcSlot))
                {
                    ManualRefresh();
                    return;
                }
            }
        }

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

            node.OnHeaderHoverChanged += (src, hovering) =>
            {
                HoverCoordinator.OnCategoryHoverChanged(CategoriesNode, src, hovering);
            };
        }
    }

    protected int CalculateOptimalItemsPerLine(float availableWidth)
        => Math.Clamp((int)MathF.Floor((availableWidth + ItemPadding) / (ItemSize + ItemPadding)), 1, 15);

    private readonly struct EffectiveWindowSizing
    {
        public InventoryWindowSizingMode Mode { get; init; }
        public float FixedWidth { get; init; }
        public float FixedHeight { get; init; }
        public float MinWidth { get; init; }
        public float MaxWidth { get; init; }
        public float MinHeight { get; init; }
        public float MaxHeight { get; init; }
    }

    private EffectiveWindowSizing GetEffectiveWindowSizing()
    {
        var settings = WindowSizingSettings;
        InventoryWindowSizingDefaults.Normalize(settings, WindowSizingLimits);

        return settings.Mode switch
        {
            InventoryWindowSizingMode.Fixed => new EffectiveWindowSizing
            {
                Mode = settings.Mode,
                FixedWidth = settings.FixedWidth,
                FixedHeight = settings.FixedHeight,
                MinWidth = MinWindowWidth,
                MaxWidth = InventoryWindowSizingDefaults.MaxConfigurableWidth,
                MinHeight = MinWindowHeight,
                MaxHeight = InventoryWindowSizingDefaults.MaxConfigurableHeight,
            },
            InventoryWindowSizingMode.CustomBounds => new EffectiveWindowSizing
            {
                Mode = settings.Mode,
                MinWidth = settings.MinWidth,
                MaxWidth = settings.MaxWidth,
                MinHeight = settings.MinHeight,
                MaxHeight = settings.MaxHeight,
            },
            _ => new EffectiveWindowSizing
            {
                Mode = InventoryWindowSizingMode.Automatic,
                MinWidth = MinWindowWidth,
                MaxWidth = MaxWindowWidth,
                MinHeight = MinWindowHeight,
                MaxHeight = MaxWindowHeight,
            }
        };
    }

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

        float gridH = contentSize.Y - ((HasFooter || HasSlotCounter) ? FooterHeight + FooterTopSpacing : 0);
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
        var sizing = GetEffectiveWindowSizing();

        if (sizing.Mode == InventoryWindowSizingMode.Fixed)
        {
            ResizeWindow(sizing.FixedWidth, sizing.FixedHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

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
            ResizeWindow(sizing.MinWidth, sizing.MinHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

        float footerSpace = HasFooter || HasSlotCounter ? FooterHeight + FooterTopSpacing : 0;

        float requiredWidth = maxChildWidth + ScrollBarWidth + (ContentStartPosition.X * 2);
        float finalWidth = Math.Clamp(requiredWidth, sizing.MinWidth, sizing.MaxWidth);

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
        float finalHeight = Math.Clamp(requiredWindowHeight, sizing.MinHeight, sizing.MaxHeight);

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
        var header = CalculateHeaderLayout();

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


    protected override unsafe void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        if (DragDropState.IsDragging) return;
        ExecuteRefresh(autosize: true);
    }


    protected override Task BuildUiAsync()
    {
        ContextMenu = new ContextMenu();

        System.AetherBagsAPI?.API.RaiseInventoryOpened();

        if (ScrollableCategories != null)
        {
            ScrollableCategories.ScrollBarNode.OnValueChanged = OnScrollValueChanged;
        }

        return Task.CompletedTask;
    }

    public async Task ToggleAsync()
    {
        if (IsOpen)
        {
            await CloseAsync();
        }
        else
        {
            await OpenAsync();
        }
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
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

    private static unsafe bool IsFocusedAddon(AtkUnitBase* addon)
    {
        var focusedAddonCount = RaptureAtkUnitManager.Instance()->FocusedUnitsList.Count;
        if (focusedAddonCount == 0) return false;

        var focusedAddon = RaptureAtkUnitManager.Instance()->FocusedUnitsList.Entries[focusedAddonCount - 1];
        if (focusedAddon.Value == null || focusedAddon.Value->Id == 0) return false;

        return focusedAddon.Value->Id == addon->Id || focusedAddon.Value->ParentId == addon->Id;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        System.AetherBagsAPI?.API.RaiseInventoryClosed();

        HoverSubscribed.Clear();
        SearchDebouncer.Dispose();
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