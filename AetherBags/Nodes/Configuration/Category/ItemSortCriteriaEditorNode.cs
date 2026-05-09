using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class ItemSortCriteriaEditorNode : VerticalListNode
{
    private const float RowHeight = 28f;
    public const float FieldWidth = 150f;
    public const float DirectionWidth = 110f;
    private const float ButtonSize = 28f;
    private const float ButtonSpacing = 4f;
    private const float AddButtonWidth = 72f;
    private const float RowWidth = FieldWidth + DirectionWidth + ButtonSize * 3f + ButtonSpacing * 4f;
    private const float AddRowWidth = FieldWidth + DirectionWidth + AddButtonWidth + ButtonSpacing * 2f;

    private readonly bool _allowUseGlobal;
    private readonly bool _allowCustomOrder;
    private readonly LabelTextNode _headerLabel;
    private readonly VerticalListNode _itemsContainer;
    private readonly HorizontalListNode _addRow;
    private readonly EnumDropDownNode<ItemSortField> _addFieldDropdown;
    private readonly EnumDropDownNode<SortDirection> _addDirectionDropdown;

    private List<ItemSortCriterion> _criteria = [];

    public Action? OnChanged { get; set; }
    public Action? OnLayoutChanged { get; set; }

    public ItemSortCriteriaEditorNode(ReadOnlySeString label, bool allowUseGlobal, bool allowCustomOrder)
    {
        _allowUseGlobal = allowUseGlobal;
        _allowCustomOrder = allowCustomOrder;

        FitContents = true;
        ItemSpacing = 4.0f;

        _headerLabel = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(RowWidth, 18),
            String = label,
            TextColor = ColorHelper.GetColor(8),
            TextTooltip = "Items are sorted by the first rule, then ties are sorted by the next rule, and so on.",
        };
        AddNode(_headerLabel);

        _itemsContainer = new VerticalListNode
        {
            Size = new Vector2(RowWidth, 0),
            ItemSpacing = 2.0f,
            FitContents = true,
            FirstItemSpacing = 2,
        };
        AddNode(_itemsContainer);

        _addRow = new HorizontalListNode
        {
            Size = new Vector2(AddRowWidth, RowHeight),
            ItemSpacing = ButtonSpacing,
        };

        _addDirectionDropdown = new EnumDropDownNode<SortDirection>
        {
            Size = new Vector2(DirectionWidth, RowHeight),
            Options = Enum.GetValues<SortDirection>().ToList(),
            SelectedOption = SortDirection.Descending,
        };

        _addFieldDropdown = new EnumDropDownNode<ItemSortField>
        {
            Size = new Vector2(FieldWidth, RowHeight),
            OnOptionSelected = field => _addDirectionDropdown.IsEnabled = field != ItemSortField.UseGlobal,
        };
        _addRow.AddNode(_addFieldDropdown);
        _addRow.AddNode(_addDirectionDropdown);

        _addRow.AddNode(new TextButtonNode
        {
            Size = new Vector2(AddButtonWidth, RowHeight),
            String = "Add",
            OnClick = AddSelectedCriterion,
        });
        _addRow.RecalculateLayout();
        AddNode(_addRow);

        RefreshAddOptions();
        RecalculateLayout();
    }

    public List<ItemSortCriterion> GetCriteria() => _criteria
        .Select(criterion => new ItemSortCriterion
        {
            Field = criterion.Field,
            Direction = criterion.Direction,
        })
        .ToList();

    public void SetCriteria(List<ItemSortCriterion>? criteria)
    {
        _criteria = CategorySettings.NormalizeItemSortCriteria(criteria, _allowUseGlobal);
        RefreshItems();
    }

    private List<ItemSortField> GetAvailableFields(ItemSortField? currentField = null)
    {
        var usedFields = _criteria
            .Select(criterion => criterion.Field)
            .Where(field => currentField is null || field != currentField.Value)
            .ToHashSet();

        return Enum.GetValues<ItemSortField>()
            .Where(field => (_allowUseGlobal || field != ItemSortField.UseGlobal)
                            && (_allowCustomOrder || field != ItemSortField.CustomOrder)
                            && (!currentField.HasValue || currentField.Value == field || !usedFields.Contains(field)))
            .ToList();
    }

    private void AddSelectedCriterion()
    {
        var fields = GetAvailableFields();
        if (fields.Count == 0) return;

        var field = _addFieldDropdown.SelectedOption;
        if (!fields.Contains(field))
            field = fields[0];

        if (!Enum.IsDefined(field)) return;

        if (field == ItemSortField.UseGlobal)
        {
            _criteria = [new ItemSortCriterion { Field = ItemSortField.UseGlobal, Direction = SortDirection.Ascending }];
        }
        else if (_criteria.All(criterion => criterion.Field != field))
        {
            _criteria.RemoveAll(criterion => criterion.Field == ItemSortField.UseGlobal);
            _criteria.Add(new ItemSortCriterion
            {
                Field = field,
                Direction = _addDirectionDropdown.SelectedOption,
            });
        }

        RefreshItems();
        OnChanged?.Invoke();
    }

    private unsafe void RefreshItems()
    {
        _itemsContainer.Clear();

        for (int i = 0; i < _criteria.Count; i++)
        {
            int index = i;
            _itemsContainer.AddNode(new ItemSortCriterionItemNode(
                _criteria[i],
                GetAvailableFields(_criteria[i].Field),
                isFirst: i == 0,
                isLast: i == _criteria.Count - 1)
            {
                Size = new Vector2(RowWidth, RowHeight),
                OnFieldChanged = field => UpdateField(index, field),
                OnDirectionChanged = direction => UpdateDirection(index, direction),
                OnMoveUp = () => MoveCriterion(index, -1),
                OnMoveDown = () => MoveCriterion(index, +1),
                OnRemove = () => RemoveCriterion(index),
            });
        }

        if (_criteria.Count == 0)
            _itemsContainer.Height = 0;

        _itemsContainer.RecalculateLayout();
        RefreshAddOptions();
        _addRow.RecalculateLayout();
        RecalculateLayout();
        OnLayoutChanged?.Invoke();

        var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
        if (addon is not null)
            addon->UpdateCollisionNodeList(false);
    }

    private void RefreshAddOptions()
    {
        var fields = GetAvailableFields();
        _addFieldDropdown.Options = fields;
        _addFieldDropdown.IsEnabled = fields.Count > 0;
        _addDirectionDropdown.IsEnabled = fields.Count > 0 && _addFieldDropdown.SelectedOption != ItemSortField.UseGlobal;
    }

    private void UpdateField(int index, ItemSortField field)
    {
        if (index < 0 || index >= _criteria.Count) return;

        if (field == ItemSortField.UseGlobal)
        {
            _criteria = [new ItemSortCriterion { Field = ItemSortField.UseGlobal, Direction = SortDirection.Ascending }];
        }
        else
        {
            var criterion = _criteria[index];
            _criteria.RemoveAt(index);
            _criteria.RemoveAll(existing => existing.Field == field || existing.Field == ItemSortField.UseGlobal);
            criterion.Field = field;
            _criteria.Insert(Math.Min(index, _criteria.Count), criterion);
            _criteria.RemoveAll(existing => existing.Field == ItemSortField.UseGlobal);
        }

        RefreshItems();
        OnChanged?.Invoke();
    }

    private void UpdateDirection(int index, SortDirection direction)
    {
        if (index < 0 || index >= _criteria.Count) return;

        _criteria[index].Direction = direction;
        RefreshItems();
        OnChanged?.Invoke();
    }

    private void RemoveCriterion(int index)
    {
        if (index < 0 || index >= _criteria.Count) return;

        _criteria.RemoveAt(index);
        Services.Framework.RunOnTick(() =>
        {
            RefreshItems();
            OnChanged?.Invoke();
        }, delayTicks: 2);
    }

    private void MoveCriterion(int index, int delta)
    {
        int target = index + delta;
        if (index < 0 || index >= _criteria.Count || target < 0 || target >= _criteria.Count) return;

        (_criteria[index], _criteria[target]) = (_criteria[target], _criteria[index]);
        Services.Framework.RunOnTick(() =>
        {
            RefreshItems();
            OnChanged?.Invoke();
        }, delayTicks: 2);
    }
}

public sealed class ItemSortCriterionItemNode : HorizontalListNode
{
    public Action<ItemSortField>? OnFieldChanged { get; init; }
    public Action<SortDirection>? OnDirectionChanged { get; init; }
    public Action? OnMoveUp { get; init; }
    public Action? OnMoveDown { get; init; }
    public Action? OnRemove { get; init; }

    public ItemSortCriterionItemNode(ItemSortCriterion criterion, List<ItemSortField> fieldOptions, bool isFirst, bool isLast)
    {
        ItemSpacing = 4.0f;

        var fieldDropdown = new EnumDropDownNode<ItemSortField>
        {
            Size = new Vector2(ItemSortCriteriaEditorNode.FieldWidth, 28),
            Options = fieldOptions,
            SelectedOption = criterion.Field,
            OnOptionSelected = field => OnFieldChanged?.Invoke(field),
        };
        AddNode(fieldDropdown);

        var directionDropdown = new EnumDropDownNode<SortDirection>
        {
            Size = new Vector2(ItemSortCriteriaEditorNode.DirectionWidth, 28),
            Options = Enum.GetValues<SortDirection>().ToList(),
            SelectedOption = criterion.Direction,
            IsEnabled = criterion.Field != ItemSortField.UseGlobal,
            OnOptionSelected = direction => OnDirectionChanged?.Invoke(direction),
        };
        AddNode(directionDropdown);

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.UpArrow,
            TextTooltip = "Move up",
            IsEnabled = !isFirst,
            OnClick = () => OnMoveUp?.Invoke(),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.ArrowDown,
            TextTooltip = "Move down",
            IsEnabled = !isLast,
            OnClick = () => OnMoveDown?.Invoke(),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.Cross,
            TextTooltip = "Remove",
            OnClick = () => OnRemove?.Invoke(),
        });
    }
}
