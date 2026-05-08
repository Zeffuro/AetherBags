using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Extensions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategorySourceOrderEditorNode : VerticalListNode
{
    private const float RowHeight = 28f;
    private const float SourceLabelWidth = 180f;
    private const float ButtonSize = 28f;
    private const float ButtonSpacing = 4f;
    private const float ButtonAreaWidth = ButtonSize * 2f + ButtonSpacing;
    private const float RowWidth = SourceLabelWidth + ButtonAreaWidth;

    private List<CategorySource> _order = CategorySettings.GetDefaultCategorySourceOrder();
    private readonly VerticalListNode _itemsContainer;

    public Action? OnChanged { get; set; }

    public CategorySourceOrderEditorNode()
    {
        FitContents = true;
        ItemSpacing = 4.0f;

        AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(RowWidth, 18),
            String = "Category Source Display Order:",
            TextColor = ColorHelper.GetColor(8),
            TextTooltip = "Use the arrows to choose which category sources are shown first. This changes display order only, not which category claims an item.",
        });

        _itemsContainer = new VerticalListNode
        {
            Size = new Vector2(RowWidth, 0),
            ItemSpacing = 2.0f,
            FitContents = true,
            FirstItemSpacing = 2,
        };
        AddNode(_itemsContainer);
    }

    public List<CategorySource> GetOrder() => _order.ToList();

    public void SetOrder(List<CategorySource> newOrder)
    {
        _order = Normalize(newOrder);
        RefreshItems();
    }

    private static List<CategorySource> Normalize(List<CategorySource> order)
    {
        var normalized = new List<CategorySource>(CategorySettings.GetDefaultCategorySourceOrder().Count);

        foreach (var source in order)
        {
            if (Enum.IsDefined(source) && !normalized.Contains(source))
                normalized.Add(source);
        }

        foreach (var source in CategorySettings.GetDefaultCategorySourceOrder())
        {
            if (!normalized.Contains(source))
                normalized.Add(source);
        }

        return normalized;
    }

    private unsafe void RefreshItems()
    {
        _itemsContainer.Clear();

        for (int i = 0; i < _order.Count; i++)
        {
            CategorySource source = _order[i];
            _itemsContainer.AddNode(CreateItemNode(source, isFirst: i == 0, isLast: i == _order.Count - 1));
        }

        _itemsContainer.RecalculateLayout();
        RecalculateLayout();

        var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
        if (addon is not null)
            addon->UpdateCollisionNodeList(false);
    }

    private CategorySourceOrderItemNode CreateItemNode(CategorySource source, bool isFirst, bool isLast) => new(source, isFirst, isLast, SourceLabelWidth, ButtonSize, ButtonSpacing)
    {
        Size = new Vector2(RowWidth, RowHeight),
        OnMoveUp = () => MoveSource(source, -1),
        OnMoveDown = () => MoveSource(source, +1),
    };

    private void MoveSource(CategorySource source, int delta)
    {
        int index = _order.IndexOf(source);
        if (index < 0) return;

        int target = index + delta;
        if (target < 0 || target >= _order.Count) return;

        (_order[index], _order[target]) = (_order[target], _order[index]);
        Services.Framework.RunOnTick(() =>
        {
            RefreshItems();
            OnChanged?.Invoke();
        }, delayTicks: 2);
    }
}

public sealed class CategorySourceOrderItemNode : HorizontalListNode
{
    public Action? OnMoveUp { get; init; }
    public Action? OnMoveDown { get; init; }

    public CategorySourceOrderItemNode(CategorySource source, bool isFirst, bool isLast, float sourceLabelWidth, float buttonSize, float buttonSpacing)
    {
        ItemSpacing = buttonSpacing;

        AddNode(new LabelTextNode
        {
            Size = new Vector2(sourceLabelWidth, 28),
            Position = new Vector2(0, 0),
            String = source.Description,
            TextColor = ColorHelper.GetColor(3),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(buttonSize, buttonSize),
            Icon = ButtonIcon.UpArrow,
            TextTooltip = "Move up",
            IsEnabled = !isFirst,
            OnClick = () => OnMoveUp?.Invoke(),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(buttonSize, buttonSize),
            Icon = ButtonIcon.ArrowDown,
            TextTooltip = "Move down",
            IsEnabled = !isLast,
            OnClick = () => OnMoveDown?.Invoke(),
        });
    }
}
