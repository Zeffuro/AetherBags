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
    private LootedItemsCategoryNode _lootedCategoryNode = null!;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override void OnSetup(AtkUnitBase* addon)
    {
        InitializeBackgroundDropTarget();

        CategoriesNode = new WrappingGridNode<InventoryCategoryNodeBase>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            HorizontalSpacing = CategorySpacing,
            VerticalSpacing = CategorySpacing,
            TopPadding = 4.0f,
            BottomPadding = 4.0f,
        };
        CategoriesNode.AttachNode(this);

        _lootedCategoryNode = new LootedItemsCategoryNode
        {
            ItemsPerLine = 10,
            OnDismissItem = OnDismissLootedItem,
            OnClearAll = OnClearAllLootedItems,
        };

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
            OnInputReceived = _ => ItemRefresh(),
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

        base.OnSetup(addon);
    }

    private void OnLootedItemsChanged(IReadOnlyList<LootedItemInfo> lootedItems)
    {
        if (!IsOpen || !IsSetupComplete) return;
        UpdateLootedCategory(lootedItems);
    }

    private void UpdateLootedCategory(IReadOnlyList<LootedItemInfo> lootedItems)
    {
        _lootedCategoryNode.UpdateLootedItems(lootedItems);

        if (lootedItems.Count > 0)
        {
            if (CategoriesNode.HoistedNode != _lootedCategoryNode)
            {
                CategoriesNode.SetHoistedNode(_lootedCategoryNode);
            }
            AutoSizeWindow();
        }
        else
        {
            using (CategoriesNode.DeferRecalculateLayout())
            {
                if (CategoriesNode.HoistedNode == _lootedCategoryNode)
                {
                    CategoriesNode.SetHoistedNode(null);
                }

                CategoriesNode.RemoveNode(_lootedCategoryNode);
            }
            AutoSizeWindow();
        }
    }

    private void OnDismissLootedItem(int index)
    {
        System.LootedItemsTracker.RemoveByIndex(index);
    }

    private void OnClearAllLootedItems()
    {
        System.LootedItemsTracker.Clear();
    }

    public void ManualCurrencyRefresh()
    {
        if (!Services.ClientState.IsLoggedIn) return;
        FooterNode.RefreshCurrencies();
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
        System.LootedItemsTracker.OnLootedItemsChanged -= OnLootedItemsChanged;

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