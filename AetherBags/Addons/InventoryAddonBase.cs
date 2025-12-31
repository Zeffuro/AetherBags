using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Scanning;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract unsafe class InventoryAddonBase :  NativeAddon
{
    protected readonly InventoryCategoryHoverCoordinator HoverCoordinator = new();
    protected readonly InventoryCategoryPinCoordinator PinCoordinator = new();
    protected readonly HashSet<InventoryCategoryNode> HoverSubscribed = new();

    protected DragDropNode BackgroundDropTarget = null!;
    protected WrappingGridNode<InventoryCategoryNode> CategoriesNode = null!;
    protected TextInputWithHintNode SearchInputNode = null!;
    protected InventoryFooterNode FooterNode = null!;
    protected TextNode? SlotCounterNode { get; set; }
    protected CircleButtonNode SettingsButtonNode = null!;

    protected virtual float MinWindowWidth => 600;
    protected virtual float MaxWindowWidth => 800;
    protected virtual float MinWindowHeight => 200;
    protected virtual float MaxWindowHeight => 1000;

    protected const float CategorySpacing = 12;
    protected const float ItemSize = 40;
    protected const float ItemPadding = 4;
    protected const float FooterHeight = 28f;
    protected const float FooterTopSpacing = 4f;

    protected bool RefreshQueued;
    protected bool RefreshAutosizeQueued;
    private bool _isRefreshing;
    protected bool _isSetupComplete;

    protected abstract InventoryStateBase InventoryState { get; }

    protected virtual bool HasFooter => true;
    protected virtual bool HasPinning => true;
    protected virtual bool HasSlotCounter => false;

    public void ManualRefresh()
    {
        if (!IsOpen) return;
        if (!Services.ClientState.IsLoggedIn) return;
        if (_isRefreshing) return;
        if (!_isSetupComplete) return;

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

    public void RefreshFromLifecycle()
    {
        if (!_isSetupComplete) return;
        if (!IsOpen) return;
        if (_isRefreshing) return;

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

    protected virtual void RefreshCategoriesCore(bool autosize)
    {
        if (!_isSetupComplete)
            return;

        if (HasFooter)
        {
            FooterNode.SlotAmountText = InventoryState.GetEmptySlotsString();
            FooterNode.RefreshCurrencies();
        }

        string filter = SearchInputNode.SearchString.ExtractText();
        var categories = InventoryState.GetCategories(filter);

        float maxContentWidth = MaxWindowWidth - (ContentStartPosition.X * 2);
        int maxItemsPerLine = CalculateOptimalItemsPerLine(maxContentWidth);

        CategoriesNode.SyncWithListDataByKey<CategorizedInventory, InventoryCategoryNode, uint>(
            dataList: categories,
            getKeyFromData: categorizedInventory => categorizedInventory.Key,
            getKeyFromNode: node => node.CategorizedInventory.Key,
            updateNode: (node, data) =>
            {
                node.CategorizedInventory = data;
                node.ItemsPerLine = Math.Min(data.Items.Count, maxItemsPerLine);
            },
            createNodeMethod: _ => CreateCategoryNode());

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
        return new InventoryCategoryNode
        {
            Size = ContentSize with { Y = 120 },
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

        Services.Logger.Debug($"[BackgroundDrop] Target: {emptyLocation} -> Visual: {visualLocation} (Ref: {absoluteIndex})");

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

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        HoverSubscribed.Clear();
        RefreshQueued = false;
        RefreshAutosizeQueued = false;

        base.OnFinalize(addon);
    }
}