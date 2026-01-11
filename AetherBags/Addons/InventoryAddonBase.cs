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
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Classes.ContextMenu;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract unsafe class InventoryAddonBase : NativeAddon, IInventoryWindow
{
    protected readonly InventoryCategoryHoverCoordinator HoverCoordinator = new();
    protected readonly InventoryCategoryPinCoordinator PinCoordinator = new();
    protected readonly HashSet<InventoryCategoryNode> HoverSubscribed = new();

    protected DragDropNode BackgroundDropTarget = null!;
    protected WrappingGridNode<InventoryCategoryNodeBase> CategoriesNode = null!;
    protected TextInputWithButtonNode SearchInputNode = null!;
    protected InventoryFooterNode FooterNode = null!;
    protected TextNode? SlotCounterNode { get; set; }
    protected CircleButtonNode SettingsButtonNode = null!;

    internal ContextMenu ContextMenu = null!;

    protected virtual float MinWindowWidth => 600;
    protected virtual float MaxWindowWidth => 800;
    protected virtual float MinWindowHeight => 200;
    protected virtual float MaxWindowHeight => 1000;

    protected const float CategorySpacing = 12;
    protected const float ItemSize = 42;
    protected const float ItemPadding = 5;
    protected const float FooterHeight = 28f;
    protected const float FooterTopSpacing = 4f;
    protected const float SettingsButtonOffset = 48f;

    protected bool RefreshQueued;
    protected bool RefreshAutosizeQueued;
    protected bool IsSetupComplete;

    protected abstract InventoryStateBase InventoryState { get; }

    protected virtual bool HasFooter => true;
    protected virtual bool HasPinning => true;
    protected virtual bool HasSlotCounter => false;

    private readonly HashSet<uint> _searchMatchScratch = new();
    private bool _isRefreshing;

    private int _requestedUpdateCount;
    private int _refreshFromLifecycleCount;
    private long _lastLogTick;

    public void ManualRefresh()
    {
        if (!IsOpen) return;
        if (!Services.ClientState.IsLoggedIn) return;
        if (_isRefreshing) return;
        if (!IsSetupComplete) return;

        try
        {
            _isRefreshing = true;
            InventoryState.RefreshFromGame();
            RefreshCategoriesCore(autosize: true);
        }
        finally
        {
            _isRefreshing = false;
        }
    }


    public string GetSearchText() => SearchInputNode?.SearchString.ExtractText() ?? string.Empty;

    public InventoryStats GetStats() => InventoryState.GetStats();

    public virtual void SetSearchText(string searchText)
    {
        Services.Framework.RunOnTick(() =>
        {
            if (IsOpen) SearchInputNode.SearchString = searchText;
            RefreshCategoriesCore(autosize: true);
        }, delayTicks: 3);
    }

    public void RefreshFromLifecycle()
    {
        if (!IsSetupComplete) return;
        if (!IsOpen) return;
        if (_isRefreshing) return;

        try
        {
            _isRefreshing = true;

            _refreshFromLifecycleCount++;
            LogRefreshStats();

            InventoryState.RefreshFromGame();
            RefreshCategoriesCore(autosize: true);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    protected virtual void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        var config = System.Config.General;
        string searchText = SearchInputNode.SearchString.ExtractText();
        bool isSearching = !string.IsNullOrWhiteSpace(searchText);

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

        CategoriesNode.SyncWithListDataByKey<CategorizedInventory, InventoryCategoryNode, uint>(
            dataList: categories,
            getKeyFromData: categorizedInventory => categorizedInventory.Key,
            getKeyFromNode: node => node.CategorizedInventory.Key,
            updateNode: (node, data) =>
            {
                node.MaxWidth = maxContentWidth;
                node.SetCategoryData(data, Math.Min(data.Items.Count, maxItemsPerLine));
                node.RefreshNodeVisuals();
            },
            createNodeMethod: _ => CreateCategoryNode(maxContentWidth));

        if (HasPinning)
        {
            bool pinsChanged = PinCoordinator.ApplyPinnedStates(CategoriesNode);
            if (pinsChanged) HoverCoordinator.ResetAll(CategoriesNode);
        }

        WireHoverHandlers();

        if (autosize)
            AutoSizeWindow();
        else
        {
            LayoutContent();
            CategoriesNode.RecalculateLayout();
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
        float headerH = header->Height;

        // Center the search bar, width is 50% of header
        float searchWidth = headerW * 0.5f;
        var searchSize = new Vector2(searchWidth, 28f);

        float searchX = (headerW - searchWidth) * 0.5f;
        float itemY = header->Y + (headerH - 28f) * 0.5f;

        return new HeaderLayout
        {
            SearchPosition = new Vector2(searchX, itemY),
            SearchSize = searchSize,
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

    protected virtual InventoryCategoryNode CreateCategoryNode(float? maxWidth = null)
    {
        return new InventoryCategoryNode
        {
            Size = ContentSize with { Y = 120 },
            MaxWidth = maxWidth,
            OnRefreshRequested = ManualRefresh,
            OnDragEnd = () => InventoryOrchestrator.RefreshAll(updateMaps: true),
        };
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

        CategoriesNode.Position = contentPos;
        CategoriesNode.Size = new Vector2(contentSize.X, gridH);

        UpdateCategoryMaxWidths(contentSize.X);
    }

    private void UpdateCategoryMaxWidths(float maxWidth)
    {
        foreach (var node in CategoriesNode.Nodes)
        {
            if (node is InventoryCategoryNode categoryNode && categoryNode.MaxWidth != maxWidth)
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
            if (nodes[i] is not InventoryCategoryNode cat)
                continue;

            childCount++;
            float w = cat.Width;
            if (w > maxChildWidth) maxChildWidth = w;
        }

        if (childCount == 0)
        {
            ResizeWindow(MinWindowWidth, MinWindowHeight, recalcLayout: true);
            return;
        }

        float requiredWidth = maxChildWidth + (ContentStartPosition.X * 2);
        float finalWidth = Math.Clamp(requiredWidth, MinWindowWidth, MaxWindowWidth);

        float contentWidth = finalWidth - (ContentStartPosition.X * 2);

        float footerSpace = HasFooter || HasSlotCounter ?  FooterHeight + FooterTopSpacing : 0;
        float gridBudget = Math.Max(0f, MaxWindowHeight - footerSpace);

        CategoriesNode.Position = ContentStartPosition;
        CategoriesNode.Size = new Vector2(contentWidth, gridBudget);

        UpdateCategoryMaxWidths(contentWidth);

        CategoriesNode.RecalculateLayout();

        float requiredGridHeight = CategoriesNode.GetRequiredHeight();
        float requiredContentHeight = requiredGridHeight + footerSpace;

        float requiredWindowHeight = requiredContentHeight + ContentStartPosition.Y + ContentStartPosition.X;
        float finalHeight = Math.Clamp(requiredWindowHeight, MinWindowHeight, MaxWindowHeight);

        ResizeWindow(finalWidth, finalHeight, recalcLayout: false);
    }

    protected void ResizeWindow(float width, float height, bool recalcLayout)
    {
        SetWindowSize(width, height);

        if (BackgroundDropTarget != null)
        {
            BackgroundDropTarget.Size = ContentSize;
        }

        LayoutContent();

        if (recalcLayout)
            CategoriesNode.RecalculateLayout();
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

    /*
    protected override void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        _requestedUpdateCount++;
        LogRefreshStats();

        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        if (DragDropState.IsDragging)
            return;

        InventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }
    */

    protected override void OnSetup(AtkUnitBase* addon)
    {
        ContextMenu = new ContextMenu();

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
        ContextMenu?.Dispose();
        HoverSubscribed.Clear();
        RefreshQueued = false;
        RefreshAutosizeQueued = false;

        base.OnFinalize(addon);
    }
}