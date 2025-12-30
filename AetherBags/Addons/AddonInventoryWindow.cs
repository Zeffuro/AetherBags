using System.Numerics;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
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

        var size = new Vector2(addon->Size.X / 2.0f, 28.0f);

        var header = addon->WindowHeaderCollisionNode;

        float headerX = header->X;
        float headerY = header->Y;
        float headerW = header->Width;
        float headerH = header->Height;

        float x = headerX + (headerW - size.X) * 0.5f;
        float y = headerY + (headerH - size.Y) * 0.5f;

        _notificationNode = new InventoryNotificationNode
        {
            Position = new Vector2(WindowNode!.X - 4f, WindowNode!.Y - 32f),
            Size = new Vector2(headerW, 28f),
        };
        _notificationNode.AttachNode(this);

        SearchInputNode = new TextInputWithHintNode
        {
            Position = new Vector2(x, y),
            Size = size,
            OnInputReceived = _ => RefreshCategoriesCore(autosize: false),
        };
        SearchInputNode.AttachNode(this);

        SettingsButtonNode = new CircleButtonNode
        {
            Position = new Vector2(headerW - 48f, y),
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

    public void ManualCurrencyRefresh()
    {
        if (!Services.ClientState.IsLoggedIn) return;
        FooterNode.RefreshCurrencies();
    }

    protected override void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    public void SetNotification(InventoryNotificationInfo info)
    {
        Services.Framework.RunOnTick(() =>
        {
            if (IsOpen) _notificationNode.NotificationInfo = info;
        }, delayTicks: 1);
    }

    public void SetSearchText(string searchText)
    {
        Services.Framework.RunOnTick(() =>
        {
            if (IsOpen) SearchInputNode.SearchString = searchText;
            RefreshCategoriesCore(autosize: true);
        }, delayTicks: 1);
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