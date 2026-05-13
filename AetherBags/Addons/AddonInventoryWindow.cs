using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public unsafe class AddonInventoryWindow : InventoryAddonBase
{
    private readonly MainBagState _inventoryState = new();
    private InventoryNotificationNode _notificationNode = null!;
    private LootedItemsCategoryNode? _lootedCategoryNode;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        InitializeBackgroundDropTarget();

        ScrollableCategories = new InventoryScrollingAreaNode<WrappingGridNode<InventoryCategoryNodeBase>>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            ContentHeight = 0f,
            AutoHideScrollBar = true,
        };
        ScrollableCategories.AttachNode(this);

        CategoriesNode = ScrollableCategories.ContentNode;
        CategoriesNode.HorizontalSpacing = CategorySpacing;
        CategoriesNode.VerticalSpacing = CategorySpacing;
        CategoriesNode.TopPadding = 4.0f;
        CategoriesNode.BottomPadding = 4.0f;

        var header = CalculateHeaderLayout(addon);

        _notificationNode = new InventoryNotificationNode
        {
            Position = new Vector2(WindowNode!.X - 4f, WindowNode!.Y - 32f),
            Size = new Vector2(header.HeaderWidth, 28f),
        };
        _notificationNode.AttachNode(this);

        SearchInputNode = new TextInputWithButtonNode
        {
            Position = header.SearchPosition,
            Size = header.SearchSize,
            OnInputReceived = _ => SearchDebouncer.Run(() => Services.Framework.RunOnTick(ItemRefresh)),
            OnButtonClicked = () => InventoryAddonContextMenu.OpenMain(this)
        };
        SearchInputNode.AttachNode(this);

        SettingsButtonNode = new CircleButtonNode
        {
            Position = new Vector2(header.HeaderWidth - SettingsButtonOffset, header.HeaderY),
            Size = new Vector2(28f),
            Icon = ButtonIcon.GearCog,
            OnClick = System.AddonConfigurationWindow.Toggle
        };
        SettingsButtonNode.AttachNode(this);

        FooterNode = new InventoryFooterNode
        {
            Size = ContentSize with { Y = FooterHeight },
            SlotAmountText = _inventoryState.GetEmptySlotsString(),
        };
        FooterNode.AttachNode(this);

        LayoutContent();

        addon->SubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        System.LootedItemsTracker.OnLootedItemsChanged += OnLootedItemsChanged;

        IsSetupComplete = true;

        _inventoryState.RefreshFromGame();

        var existingLoot = System.LootedItemsTracker.LootedItems;
        if (existingLoot.Count > 0)
        {
            UpdateLootedCategory(existingLoot);
        }

        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon, atkValueSpan);
    }

    private void OnLootedItemsChanged(IReadOnlyList<LootedItemInfo> lootedItems)
    {
        if (!IsOpen || !IsSetupComplete) return;

        UpdateRecentlyLootedHighlight();
        UpdateLootedCategory(lootedItems);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        UpdateRecentlyLootedHighlight();
        UpdateLootedCategory(System.LootedItemsTracker.LootedItems);
        base.RefreshCategoriesCore(autosize);
    }

    private void UpdateRecentlyLootedHighlight()
    {
        if (System.Config.General.HighlightRecentlyLootedItems && System.LootedItemsTracker.UnseenLootItemIds.Count > 0)
        {
            var color = System.Config.General.RecentlyLootedHighlightColor;
            var rgb = new Vector3(color.X * color.W, color.Y * color.W, color.Z * color.W);
            HighlightState.SetLabel(HighlightSource.RecentlyLooted, System.LootedItemsTracker.UnseenLootItemIds, rgb);
        }
        else
        {
            HighlightState.ClearLabel(HighlightSource.RecentlyLooted);
        }
    }

    private void UpdateLootedCategory(IReadOnlyList<LootedItemInfo> lootedItems)
    {
        bool shouldShow = lootedItems.Count > 0 && System.Config.General.ShowRecentlyLooted;

        if (shouldShow)
        {
            _lootedCategoryNode ??= CreateLootedCategoryNode();
            _lootedCategoryNode.IsVisible = true;
            _lootedCategoryNode.UpdateLootedItems(lootedItems);

            if (CategoriesNode.HoistedNode != _lootedCategoryNode)
            {
                CategoriesNode.SetHoistedNode(_lootedCategoryNode);
            }
            AutoSizeWindow();
        }
        else if (_lootedCategoryNode is not null)
        {
            // Hide instead of removing. Disposing the category while its grid still holds the
            // dismissed display node's AtkComponentIcon crashes during native Deinitialize.
            // The category is torn down in OnFinalize where the event state has long settled.
            if (CategoriesNode.HoistedNode == _lootedCategoryNode)
                CategoriesNode.SetHoistedNode(null);
            _lootedCategoryNode.IsVisible = false;
            CategoriesNode.InvalidateLayout();
            AutoSizeWindow();
        }
    }

    private LootedItemsCategoryNode CreateLootedCategoryNode() => new()
    {
        ItemsPerLine = 10,
        OnDismissItem = OnDismissLootedItem,
        OnClearAll = OnClearAllLootedItems,
    };

    private void OnDismissLootedItem(int index)
    {
        System.LootedItemsTracker.RemoveByIndex(index);
        // Defer flush: removing the last item disposes _lootedCategoryNode, which would tear
        // down the AtkUldManager while we're still inside this child's click handler.
        Services.Framework.RunOnTick(() => System.LootedItemsTracker.FlushPendingChanges());
    }

    private void OnClearAllLootedItems()
    {
        System.LootedItemsTracker.Clear();
        Services.Framework.RunOnTick(() => System.LootedItemsTracker.FlushPendingChanges());
    }

    public void ManualCurrencyRefresh()
    {
        if (!Services.ClientState.IsLoggedIn) return;
        FooterNode.RefreshCurrencies();
    }

    protected override void UpdateHeaderLayout()
    {
        base.UpdateHeaderLayout();

        AtkUnitBase* addon = this;
        if (addon == null) return;

        var header = CalculateHeaderLayout(addon);

        if (_notificationNode != null)
        {
            _notificationNode.Size = new Vector2(header.HeaderWidth, 28f);
        }
    }

    public void SetNotification(InventoryNotificationInfo info)
    {
        Services.Framework.RunOnTick(() =>
        {
            if (IsOpen) _notificationNode.NotificationInfo = info;
        }, delayTicks: 3);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;
        System.LootedItemsTracker.OnLootedItemsChanged -= OnLootedItemsChanged;

        System.LootedItemsTracker.UnseenLootItemIds.Clear();
        HighlightState.ClearLabel(HighlightSource.RecentlyLooted);

        ref var blockingAddonId = ref AgentInventoryContext.Instance()->BlockingAddonId;
        if (blockingAddonId != 0)
        {
            RaptureAtkModule.Instance()->CloseAddon(blockingAddonId);
        }

        addon->UnsubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        IsSetupComplete = false;
        base.OnFinalize(addon);
    }
}
