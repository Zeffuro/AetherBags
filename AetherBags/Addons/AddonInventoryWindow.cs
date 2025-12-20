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
    private const float ItemPadding = 6;

    protected override unsafe void OnSetup(AtkUnitBase* addon)
    {
        _categoriesNode = new WrappingGridNode<InventoryCategoryNode>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            HorizontalSpacing = CategorySpacing,
            VerticalSpacing = CategorySpacing
        };
        _categoriesNode.AttachNode(this);

        var size = new Vector2(addon->Size.X / 2.0f, 28.0f);

        Vector2 headerSize = new Vector2(addon->WindowHeaderCollisionNode->Width, addon->WindowHeaderCollisionNode->Height);
        _searchInputNode = new TextInputWithHintNode {
            Position = headerSize / 2.0f - size / 2.0f + new Vector2(25.0f, 10.0f),
            Size = size,
            OnInputReceived = _ => RefreshCategories(false),
        };
        _searchInputNode.AttachNode(this);

        _footerNode = new InventoryFooterNode
        {
            Size = ContentSize with { Y = 28 },
            SlotAmountText = InventoryState.GetEmptyItemSlotsString()
        };
        _footerNode.AttachNode(this);

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Inventory", OnInventoryUpdate);
        addon->SubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        RefreshCategories();
        base.OnSetup(addon);
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        // Haven't needed it yet but just in case.
        base.OnUpdate(addon);
    }

    private void OnInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        RefreshCategories();
    }

    protected override unsafe void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData) {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);
        RefreshCategories();
    }

    private void RefreshCategories(bool autosize = true)
    {
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

        if(autosize) AutoSizeWindow();
    }

    private int CalculateOptimalItemsPerLine(float availableWidth)
    {
        return Math.Clamp((int)Math.Floor((availableWidth + ItemPadding) / (ItemSize + ItemPadding)), 1, 15);
    }

    private void AutoSizeWindow()
    {
        List<InventoryCategoryNode> childNodes = _categoriesNode.GetNodes<InventoryCategoryNode>().ToList();
        if (childNodes.Count == 0)
        {
            ResizeWindow(MinWindowWidth, MinWindowHeight);
            return;
        }

        float requiredWidth = childNodes.Max(node => node. Width);
        requiredWidth += ContentStartPosition.X * 2;
        float finalWidth = Math.Clamp(requiredWidth, MinWindowWidth, MaxWindowWidth);

        float contentWidth = finalWidth - (ContentStartPosition.X * 2);
        _categoriesNode.Size = new Vector2(contentWidth, MaxWindowHeight);

        _categoriesNode.RecalculateLayout();

        float requiredHeight = _categoriesNode.GetRequiredHeight();
        requiredHeight += ContentStartPosition.Y + ContentStartPosition.X;

        float finalHeight = Math.Clamp(requiredHeight, MinWindowHeight, MaxWindowHeight);

        ResizeWindow(finalWidth, finalHeight);
    }

    private void ResizeWindow(float width, float height)
    {
        SetWindowSize(width, height);
        _categoriesNode.Size = ContentSize;
        _footerNode.Size = ContentSize with { Y = 28 };
        _categoriesNode.RecalculateLayout();
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        base.OnFinalize(addon);
        Services.AddonLifecycle.UnregisterListener(OnInventoryUpdate);
        addon->UnsubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);
    }
}