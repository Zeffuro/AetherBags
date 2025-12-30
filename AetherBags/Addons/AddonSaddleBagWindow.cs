using System.Numerics;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public unsafe class AddonSaddleBagWindow :  InventoryAddonBase
{
    private readonly SaddleBagState _inventoryState = new();
    private TextNode _slotCounterNode = null!;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override bool HasFooter => false;
    protected override bool HasSlotCounter => true;

    protected override float MinWindowWidth => 400;
    protected override float MaxWindowWidth => 600;

    protected override void OnSetup(AtkUnitBase* addon)
    {
        WindowNode?.AddColor = new Vector3(-16f / 255f, -4f / 255f, 8f / 255f);

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

        _slotCounterNode = new TextNode
        {
            Position = new Vector2(Size.X - 10, 0),
            Size = new Vector2(82, 20),
            AlignmentType = AlignmentType.Right,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(32) // Could also be Color 65
        };
        _slotCounterNode.AttachNode(this);
        SlotCounterNode = _slotCounterNode;

        LayoutContent();

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "InventoryBuddy", OnSaddleBagUpdate);

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        _slotCounterNode.String = _inventoryState.GetEmptySlotsString();

        base.RefreshCategoriesCore(autosize);
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

    private void OnSaddleBagUpdate(AddonEvent type, AddonArgs args)
    {
        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    protected override void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
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
        Services.AddonLifecycle.UnregisterListener(OnSaddleBagUpdate);

        base.OnFinalize(addon);
    }
}