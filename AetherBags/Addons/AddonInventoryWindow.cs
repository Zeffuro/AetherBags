using System.Numerics;
using AetherBags.Inventory.Context;
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

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override void OnSetup(AtkUnitBase* addon)
    {
        InitializeBackgroundDropTarget();

        CategoriesNode = new WrappingGridNode<InventoryCategoryNode>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            HorizontalSpacing = CategorySpacing,
            VerticalSpacing = CategorySpacing,
            TopPadding = 4.0f,
            BottomPadding = 4.0f,
        };
        CategoriesNode.AttachNode(this);

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

        _isSetupComplete = true;

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon);
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
        ref var blockingAddonId = ref AgentInventoryContext.Instance()->BlockingAddonId;
        if (blockingAddonId != 0)
        {
            RaptureAtkModule.Instance()->CloseAddon(blockingAddonId);
        }

        addon->UnsubscribeAtkArrayData(1, (int)NumberArrayType.Inventory);

        _isSetupComplete = false;
        base.OnFinalize(addon);
    }
}