using System.Numerics;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
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

    private readonly Vector3 _tintColor = new (-16f / 255f, -4f / 255f, 8f / 255f);

    protected override float MinWindowWidth => 400;
    protected override float MaxWindowWidth => 600;

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
            AddColor = _tintColor,
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
            TextOutlineColor = ColorHelper.GetColor(32)
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

        base.RefreshCategoriesCore(autosize);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;

        if (System.Config.General.HideGameSaddleBags)
        {
            var saddleAddon = RaptureAtkUnitManager.Instance()->GetAddonByName("InventoryBuddy");
            if (saddleAddon != null)
            {
                saddleAddon->IsVisible = true;
                saddleAddon->Close(true);
            }
        }

        base.OnFinalize(addon);
    }
}