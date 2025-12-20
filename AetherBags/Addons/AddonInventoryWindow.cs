using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Extensions;
using AetherBags.Inventory;
using AetherBags.Nodes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using Lumina.Data.Parsing.Uld;

namespace AetherBags.Addons;

public class AddonInventoryWindow : NativeAddon
{
    private readonly InventoryCategoryHoverCoordinator _hoverCoordinator = new();
    private readonly HashSet<InventoryCategoryNode> _hoverSubscribed = new();

    private WrappingGridNode<InventoryCategoryNode> _categoriesNode = null!;
    private TextInputWithHintNode _searchInputNode = null!;
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

        Vector2 headerSize = new Vector2(addon->WindowHeaderCollisionNode->Width, addon->WindowHeaderCollisionNode->Height);
        _searchInputNode = new TextInputWithHintNode
        {
            Position = headerSize / 2.0f - size / 2.0f + new Vector2(25.0f, 10.0f),
            Size = size,
            OnInputReceived = _ => RefreshCategories(false),
        };
        _searchInputNode.AttachNode(this);

        _footerNode = new InventoryFooterNode
        {
            Size = ContentSize with { Y = FooterHeight },
            SlotAmountText = InventoryState.GetEmptyItemSlotsString()
        };
        _footerNode.AttachNode(this);

        LayoutContent();

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Inventory", OnInventoryUpdate);
        addon->SubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        RefreshCategories();
        base.OnSetup(addon);
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        base.OnUpdate(addon);
    }

    private void OnInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        RefreshCategories();
    }

    protected override unsafe void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);
        RefreshCategories();
    }

    private void RefreshCategories(bool autosize = true)
    {
        _footerNode.SlotAmountText = InventoryState.GetEmptyItemSlotsString();

        var categories = InventoryState.GetInventoryItemCategories(_searchInputNode.SearchString.ExtractText());

        float maxContentWidth = MaxWindowWidth - (ContentStartPosition.X * 2);
        int maxItemsPerLine = CalculateOptimalItemsPerLine(maxContentWidth);

        _categoriesNode.SyncWithListData(
            categories,
            node => node.CategorizedInventory,
            data => new InventoryCategoryNode
            {
                Size = ContentSize with { Y = 120 },
                CategorizedInventory = data,
                ItemsPerLine = Math.Min(data.Items.Count, maxItemsPerLine)
            });

        foreach (InventoryCategoryNode node in _categoriesNode.GetNodes<InventoryCategoryNode>())
        {
            node.ItemsPerLine = Math.Min(node.CategorizedInventory.Items.Count, maxItemsPerLine);
        }

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
        List<InventoryCategoryNode> categoryNodes = _categoriesNode.GetNodes<InventoryCategoryNode>().ToList();

        for (int i = 0; i < categoryNodes.Count; i++)
        {
            InventoryCategoryNode node = categoryNodes[i];

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
        return Math.Clamp((int)Math.Floor((availableWidth + ItemPadding) / (ItemSize + ItemPadding)), 1, 15);
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
        List<InventoryCategoryNode> childNodes = _categoriesNode.GetNodes<InventoryCategoryNode>().ToList();
        if (childNodes.Count == 0)
        {
            ResizeWindow(MinWindowWidth, MinWindowHeight);
            return;
        }

        float requiredWidth = childNodes.Max(node => node.Width);
        requiredWidth += ContentStartPosition.X * 2;
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

        ResizeWindow(finalWidth, finalHeight);
    }

    private void ResizeWindow(float width, float height)
    {
        SetWindowSize(width, height);

        LayoutContent();

        _categoriesNode.RecalculateLayout();
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        base.OnFinalize(addon);
        Services.AddonLifecycle.UnregisterListener(OnInventoryUpdate);
        addon->UnsubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        _hoverSubscribed.Clear();
    }
}
