using System.Linq;
using System.Numerics;
using AetherBags.Inventory;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
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

    private readonly string[] _retainerAddonNames = { "InventoryRetainer", "InventoryRetainerLarge" };

    protected override void OnSetup(AtkUnitBase* addon)
    {
        InitializeBackgroundDropTarget();

        WindowNode?.AddColor = _tintColor;

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

        var header = CalculateHeaderLayout(addon);

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
        IsSetupComplete = true;

        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
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

    private void CloseRetainerWindows()
    {
        var manager = RaptureAtkUnitManager.Instance();
        foreach (var name in _retainerAddonNames)
        {
            var addon = manager->GetAddonByName(name);
            if (addon != null)
            {
                addon->IsVisible = true;
                addon->Close(true);
            }
        }
    }

    private bool IsAnyRetainerWindowLoaded()
    {
        return _retainerAddonNames.Any(name => RaptureAtkUnitManager.Instance()->GetAddonByName(name) != null);
    }

    protected override void OnShow(AtkUnitBase* addon)
    {
        base.OnShow(addon);

        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void OnEntrustDuplicates()
    {
        if (!IsAnyRetainerWindowLoaded()) return;
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        agent->SendCommand(0, [0]);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;

        CloseRetainerWindows();

        base.OnFinalize(addon);
    }
}