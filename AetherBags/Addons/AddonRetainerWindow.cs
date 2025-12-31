using System.Numerics;
using AetherBags.Inventory;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public unsafe class AddonRetainerWindow : InventoryAddonBase
{
    private readonly RetainerState _inventoryState = new();
    private TextNode _slotCounterNode = null!;
    private TextNode _retainerNameNode = null!;
    private TextButtonNode _entrustDuplicatesButton = null!;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override bool HasFooter => false;
    protected override bool HasSlotCounter => true;

    private readonly Vector3 _tintColor = new(8f / 255f, -8f / 255f, -4f / 255f);

    protected override float MinWindowWidth => 400;
    protected override float MaxWindowWidth => 700;

    protected override void OnSetup(AtkUnitBase* addon)
    {
        InitializeBackgroundDropTarget();

        WindowNode?.AddColor = _tintColor;

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

        _retainerNameNode = new TextNode
        {
            Position = new Vector2(8f, 0),
            Size = new Vector2(200, 20),
            AlignmentType = AlignmentType.Left,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(32),
        };
        _retainerNameNode.AttachNode(this);

        _entrustDuplicatesButton = new TextButtonNode
        {
            Size = new Vector2(120, 28),
            AddColor = _tintColor,
            String = "Entrust Duplicates",
            OnClick = OnEntrustDuplicates,
        };
        _entrustDuplicatesButton.AttachNode(this);

        // Slot counter
        _slotCounterNode = new TextNode
        {
            Position = new Vector2(Size.X - 10, 0),
            Size = new Vector2(82, 20),
            AlignmentType = AlignmentType.Right,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(32),
        };
        _slotCounterNode.AttachNode(this);
        SlotCounterNode = _slotCounterNode;

        LayoutContent();

        _inventoryState.RefreshFromGame();
        _isSetupComplete = true;

        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        if (!_isSetupComplete)
            return;

        _slotCounterNode.String = _inventoryState.GetEmptySlotsString();
        _retainerNameNode.String = RetainerState.CurrentRetainerName;

        base.RefreshCategoriesCore(autosize);
    }

    protected override void LayoutContent()
    {
        base.LayoutContent();

        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerY = contentPos.Y + contentSize.Y - FooterHeight + 4f;

        _retainerNameNode.Position = new Vector2(contentPos.X + 8f, footerY);

        float buttonWidth = _entrustDuplicatesButton.Width;
        float buttonX = contentPos.X + (contentSize.X - buttonWidth) / 2f;
        _entrustDuplicatesButton.Position = new Vector2(buttonX, footerY - 2f);

        if (SlotCounterNode != null)
            SlotCounterNode.Position = new Vector2(contentSize.X - 80f, footerY);
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

    protected override void OnShow(AtkUnitBase* addon)
    {
        base.OnShow(addon);

        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void OnEntrustDuplicates()
    {
        // TODO: Implement checking if the retainer bag is def open
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        agent->SendCommand(0, [0]);
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
        _isSetupComplete = false;
        base.OnFinalize(addon);
    }
}