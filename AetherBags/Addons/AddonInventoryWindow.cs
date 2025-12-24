using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Extensions;
using AetherBags.Inventory;
using AetherBags.Nodes;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public class AddonInventoryWindow : NativeAddon
{
    private readonly InventoryCategoryHoverCoordinator _hoverCoordinator = new();
    private readonly HashSet<InventoryCategoryNode> _hoverSubscribed = new();

    private WrappingGridNode<InventoryCategoryNode> _categoriesNode = null!;
    private TextInputWithHintNode _searchInputNode = null!;
    private CircleButtonNode _settingsButtonNode = null!;
    private InventoryFooterNode _footerNode = null!;

    // Window constraints
    private const float MinWindowWidth = 300;
    private const float MaxWindowWidth = 800;
    private const float MinWindowHeight = 200;
    private const float MaxWindowHeight = 1000;

    // Layout settings
    private const float CategorySpacing = 12;
    private const float ItemSize = 40;
    private const float ItemPadding = 4;

    private const float FooterHeight = 28f;
    private const float FooterTopSpacing = 4f;

    private bool _refreshQueued;
    private bool _refreshAutosizeQueued;

    protected override unsafe void OnSetup(AtkUnitBase* addon)
    {
        _categoriesNode = new WrappingGridNode<InventoryCategoryNode>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            HorizontalSpacing = CategorySpacing,
            VerticalSpacing = CategorySpacing,
            TopPadding = 4.0f,
            BottomPadding = 4.0f,
        };
        _categoriesNode.AttachNode(this);

        var size = new Vector2(addon->Size.X / 2.0f, 28.0f);

        var header = addon->WindowHeaderCollisionNode;

        float headerX = header->X;
        float headerY = header->Y;
        float headerW = header->Width;
        float headerH = header->Height;

        float x = headerX + (headerW - size.X) * 0.5f;
        float y = headerY + (headerH - size.Y) * 0.5f;

        _searchInputNode = new TextInputWithHintNode
        {
            Position = new Vector2(x, y),
            Size = size,
            OnInputReceived = _ => RefreshCategoriesCore(autosize: false),
        };
        _searchInputNode.AttachNode(this);

        _settingsButtonNode = new CircleButtonNode
        {
            Position = new Vector2(headerW - 48f, y),
            Size = new Vector2(28f),
            Icon = ButtonIcon.GearCog,
            OnClick = System.AddonConfigurationWindow.Toggle
        };
        _settingsButtonNode.AttachNode(this);

        _footerNode = new InventoryFooterNode
        {
            Size = ContentSize with { Y = FooterHeight },
            SlotAmountText = InventoryState.GetEmptyItemSlotsString(),
        };
        _footerNode.AttachNode(this);

        LayoutContent();

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Inventory", OnInventoryUpdate);
        addon->SubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        InventoryState.RefreshFromGame();

        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon);
    }


    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        if (_refreshQueued)
        {
            bool doAutosize = _refreshAutosizeQueued;
            _refreshQueued = false;
            _refreshAutosizeQueued = false;

            RefreshCategoriesCore(doAutosize);
        }

        base.OnUpdate(addon);
    }

    public void ManualInventoryRefresh()
    {
        InventoryState.RefreshFromGame();
        RefreshCategoriesCore(true);
    }

    public void ManualCurrencyRefresh()
    {
        _footerNode.RefreshCurrencies();
    }

    private void OnInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        InventoryState.RefreshFromGame();

        RefreshCategoriesCore(autosize: true);
    }

    protected override unsafe void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        InventoryState.RefreshFromGame();

        RefreshCategoriesCore(autosize: true);
    }

    private void RefreshCategoriesCore(bool autosize)
    {
        _footerNode.SlotAmountText = InventoryState.GetEmptyItemSlotsString();
        _footerNode.RefreshCurrencies();

        string filter = _searchInputNode.SearchString.ExtractText();
        IReadOnlyList<CategorizedInventory> categories = InventoryState.GetInventoryItemCategories(filter);

        float maxContentWidth = MaxWindowWidth - (ContentStartPosition.X * 2);
        int maxItemsPerLine = CalculateOptimalItemsPerLine(maxContentWidth);

        _categoriesNode.SyncWithListDataByKey<CategorizedInventory, InventoryCategoryNode, uint>(
            dataList: categories,
            getKeyFromData: c => c.Key,
            getKeyFromNode: n => n.CategorizedInventory.Key,
            updateNode: (node, data) =>
            {
                node.CategorizedInventory = data;
                node.ItemsPerLine = Math.Min(data.Items.Count, maxItemsPerLine);
            },
            createNodeMethod: _ => new InventoryCategoryNode
            {
                Size = ContentSize with { Y = 120 },
            });

        WireHoverHandlers();

        if (autosize) AutoSizeWindow();
        else
        {
            LayoutContent();
            _categoriesNode.RecalculateLayout();
        }
    }

    private void WireHoverHandlers()
    {
        var nodes = _categoriesNode.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryCategoryNode node)
                continue;

            if (!_hoverSubscribed.Add(node))
                continue;

            node.HeaderHoverChanged += (src, hovering) =>
            {
                _hoverCoordinator.OnCategoryHoverChanged(_categoriesNode, src, hovering);
            };
        }
    }

    private int CalculateOptimalItemsPerLine(float availableWidth)
    {
        return Math.Clamp((int)MathF.Floor((availableWidth + ItemPadding) / (ItemSize + ItemPadding)), 1, 15);
    }

    private void LayoutContent()
    {
        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerH = FooterHeight;

        _footerNode.Position = new Vector2(contentPos.X, contentPos.Y + contentSize.Y - footerH);
        _footerNode.Size = new Vector2(contentSize.X, footerH);

        float gridH = contentSize.Y - footerH - FooterTopSpacing;
        if (gridH < 0) gridH = 0;

        _categoriesNode.Position = contentPos;
        _categoriesNode.Size = new Vector2(contentSize.X, gridH);
    }

    private void AutoSizeWindow()
    {
        var nodes = _categoriesNode.Nodes;

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

        float gridBudget = Math.Max(0f, MaxWindowHeight - FooterHeight - FooterTopSpacing);

        _categoriesNode.Position = ContentStartPosition;
        _categoriesNode.Size = new Vector2(contentWidth, gridBudget);

        _categoriesNode.RecalculateLayout();

        float requiredGridHeight = _categoriesNode.GetRequiredHeight();
        float requiredContentHeight = requiredGridHeight + FooterTopSpacing + FooterHeight;

        float requiredWindowHeight = requiredContentHeight + ContentStartPosition.Y + ContentStartPosition.X;
        float finalHeight = Math.Clamp(requiredWindowHeight, MinWindowHeight, MaxWindowHeight);

        ResizeWindow(finalWidth, finalHeight, recalcLayout: false);
    }

    private void ResizeWindow(float width, float height, bool recalcLayout)
    {
        SetWindowSize(width, height);
        LayoutContent();

        if (recalcLayout)
            _categoriesNode.RecalculateLayout();
    }

    private void ResizeWindow(float width, float height)
        => ResizeWindow(width, height, recalcLayout: true);

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        Services.AddonLifecycle.UnregisterListener(OnInventoryUpdate);
        addon->UnsubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        _hoverSubscribed.Clear();
        _refreshQueued = false;
        _refreshAutosizeQueued = false;

        base.OnFinalize(addon);
    }
}
