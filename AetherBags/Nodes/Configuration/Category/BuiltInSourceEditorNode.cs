using System.Linq;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.IPC.ExternalCategorySystem;
using AetherBags.Nodes.Color;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using KamiToolKit.Premade.Node.Simple;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class BuiltInSourceEditorNode : SimpleComponentNode
{
    public Action? OnLayoutChanged { get; init; }

    private readonly ScrollingAreaNode<VerticalListNode> _scrollingArea;
    private readonly TextNode _headerLabel;
    private readonly TextNode _subLabel;
    private readonly CheckboxNode _enabledCheckbox;
    private readonly CheckboxNode _pinCheckbox;
    private readonly ColorInputRow _colorInput;
    private readonly NumericInputNode _priorityInput;
    private readonly NumericInputNode _orderInput;
    private readonly ItemSortCriteriaEditorNode _sortEditor;
    private readonly UintListEditorNode _customOrderEditor;
    private AddonItemPicker? _itemPicker;

    private IExternalItemSource? _source;
    private BuiltInSourceOverride? _override;
    private bool _isRefreshing;

    public BuiltInSourceEditorNode()
    {
        _scrollingArea = new ScrollingAreaNode<VerticalListNode>
        {
            AutoHideScrollBar = true,
            ContentHeight = 100f,
        };
        _scrollingArea.AttachNode(this);

        var list = _scrollingArea.ContentAreaNode;
        list.FitContents = true;
        list.ItemSpacing = 4.0f;

        _headerLabel = new TextNode
        {
            Size = new Vector2(400, 28),
            FontSize = 18,
            FontType = FontType.MiedingerMed,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(7),
            TextFlags = TextFlags.Edge,
        };
        list.AddNode(_headerLabel);

        _subLabel = new TextNode
        {
            Size = new Vector2(400, 18),
            FontSize = 12,
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(3),
        };
        list.AddNode(_subLabel);

        list.AddNode(new ResNode { Height = 8 });

        _enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            String = "Enabled",
            OnClick = isChecked =>
            {
                if (_isRefreshing || _override is null) return;
                _override.Enabled = isChecked;
                NotifyChanged();
            },
        };
        list.AddNode(_enabledCheckbox);

        _pinCheckbox = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            String = "Pinned",
            OnClick = isChecked =>
            {
                if (_isRefreshing || _override is null) return;
                _override.Pinned = isChecked;
                NotifyChanged();
            },
        };
        list.AddNode(_pinCheckbox);

        _colorInput = new ColorInputRow
        {
            Label = "Color",
            Size = new Vector2(300, 28),
            CurrentColor = ColorHelper.GetColor(50),
            DefaultColor = ColorHelper.GetColor(50),
            OnColorConfirmed = c => ApplyColor(c),
            OnColorCanceled = c => ApplyColor(c),
            OnColorPreviewed = c => ApplyColor(c),
            OnColorChange = c => ApplyColor(c),
        };
        list.AddNode(_colorInput);

        list.AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(80, 20),
            String = "Priority:",
        });
        _priorityInput = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = 1000,
            Step = 1,
            OnValueUpdate = value =>
            {
                if (_isRefreshing || _override is null) return;
                _override.Priority = value;
                NotifyChanged();
            },
        };
        list.AddNode(_priorityInput);

        list.AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(80, 20),
            String = "Order:",
        });
        _orderInput = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = 9999,
            Step = 1,
            OnValueUpdate = value =>
            {
                if (_isRefreshing || _override is null) return;
                _override.Order = value;
                NotifyChanged();
            },
        };
        list.AddNode(_orderInput);

        list.AddNode(new ResNode { Height = 8 });

        _sortEditor = new ItemSortCriteriaEditorNode("Item Sort Priority:", allowUseGlobal: true, allowCustomOrder: true);
        _sortEditor.OnLayoutChanged = HandleLayoutChange;
        _sortEditor.OnChanged = () =>
        {
            if (_isRefreshing || _override is null) return;
            _override.ItemSortCriteria = _sortEditor.GetCriteria();
            NotifyChanged();
            RefreshCustomOrderVisibility();
        };
        list.AddNode(_sortEditor);

        _customOrderEditor = new UintListEditorNode
        {
            Label = "Custom Item Order:",
            MaxValue = Services.DataManager.GetExcelSheet<Item>().LastOrDefault().RowId,
            LabelResolver = CategoryDefinitionConfigurationNode.ResolveItemName,
            OnSearchButtonClicked = OpenItemPicker,
            OnChanged = () =>
            {
                if (_isRefreshing || _override is null) return;
                _override.CustomItemOrder = _customOrderEditor.GetList();
                NotifyChanged();
                HandleLayoutChange();
            },
        };
        list.AddNode(_customOrderEditor);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _scrollingArea.Position = Vector2.Zero;
        _scrollingArea.Size = Size;
        HandleLayoutChange();
    }

    public void SetSource(IExternalItemSource source, BuiltInSourceOverride preferences)
    {
        _source = source;
        _override = preferences;

        _isRefreshing = true;
        try
        {
            _headerLabel.String = source.DisplayName;
            _subLabel.String = source.IsBuiltIn ? "★ Built-in Source" : "✦ External Source";

            var defaults = ResolveSourceDefaults(source);
            _enabledCheckbox.IsChecked = preferences.Enabled;
            _pinCheckbox.IsChecked = preferences.Pinned ?? defaults.IsPinned;
            var color = preferences.Color ?? defaults.Color;
            _colorInput.CurrentColor = color;
            _colorInput.DefaultColor = defaults.Color;
            _priorityInput.Value = preferences.Priority ?? 100;
            _orderInput.Value = preferences.Order ?? defaults.Order;

            _sortEditor.SetCriteria(preferences.ItemSortCriteria);
            _customOrderEditor.SetList(preferences.CustomItemOrder);
            RefreshCustomOrderVisibility();
        }
        finally
        {
            _isRefreshing = false;
        }

        HandleLayoutChange();
    }

    private void ApplyColor(Vector4 color)
    {
        if (_isRefreshing || _override is null) return;
        _override.Color = color;
        NotifyChanged();
    }

    private void RefreshCustomOrderVisibility()
    {
        _customOrderEditor.IsVisible = _sortEditor.GetCriteria().Any(c => c.Field == ItemSortField.CustomOrder);
        HandleLayoutChange();
    }

    private void HandleLayoutChange()
    {
        _scrollingArea.ContentAreaNode.RecalculateLayout();
        _scrollingArea.ContentHeight = _scrollingArea.ContentAreaNode.Height;
        OnLayoutChanged?.Invoke();
    }

    private void OpenItemPicker()
    {
        _itemPicker ??= new AddonItemPicker
        {
            Title = "Select Items to Order",
            InternalName = "Aetherbags_OrderItemPicker",
            SearchOptions = Services.DataManager.GetExcelSheet<Item>()
                .Where(i => i.RowId > 0 && !i.Name.IsEmpty)
                .ToList(),
            SortingOptions = [DefaultSortOptions.Alphabetical, DefaultSortOptions.Id],
            ItemSpacing = 3.0f,
        };

        _itemPicker.SelectionResult = item => _customOrderEditor.AddValue(item.RowId);
        ItemListItemWithAddNode.OnAddClicked = item => _customOrderEditor.AddValue(item.RowId);
        _itemPicker.Open();
    }

    private static (Vector4 Color, bool IsPinned, int Order) ResolveSourceDefaults(IExternalItemSource source)
    {
        var assignments = source.GetCategoryAssignments();
        if (assignments is not null)
        {
            foreach (var kvp in assignments)
            {
                var a = kvp.Value;
                return (a.CategoryColor, a.IsPinned, a.SubPriority);
            }
        }
        return (ColorHelper.GetColor(50), false, 0);
    }

    private static void NotifyChanged()
    {
        Util.SaveConfig(System.Config);
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }
}
