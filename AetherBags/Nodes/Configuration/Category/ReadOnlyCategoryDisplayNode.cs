using System;
using System.Numerics;
using AetherBags.Addons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class ReadOnlyCategoryDisplayNode : SimpleComponentNode
{
    private readonly TextNode _headerLabel;
    private readonly TextNode _kindLabel;
    private readonly TextNode _explanationLabel;
    private readonly HoldButtonNode _overrideButton;

    private CategoryWrapper? _wrapper;

    public Action<uint>? OnOverrideRequested { get; set; }

    public ReadOnlyCategoryDisplayNode()
    {
        _headerLabel = new TextNode
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(400, 28),
            FontSize = 18,
            FontType = FontType.MiedingerMed,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(7),
            TextFlags = TextFlags.Edge,
        };
        _headerLabel.AttachNode(this);

        _kindLabel = new TextNode
        {
            Position = new Vector2(0, 32),
            Size = new Vector2(400, 20),
            FontSize = 12,
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(3),
        };
        _kindLabel.AttachNode(this);

        _explanationLabel = new TextNode
        {
            Position = new Vector2(0, 64),
            Size = new Vector2(400, 96),
            FontSize = 13,
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(8),
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
            LineSpacing = 18,
            String = "This is a built-in game category. To customize it (rename, " +
                    "recolor, change items, pin), you must first override it.\n\n" +
                    "Overriding creates an editable user copy that takes over those items. " +
                    "The original will be hidden from your list until the override is deleted.",
        };
        _explanationLabel.AttachNode(this);

        _overrideButton = new HoldButtonNode
        {
            Position = new Vector2(0, 172),
            Size = new Vector2(180, 32),
            String = "Override",
            TextTooltip = "Hold to create an editable user copy that overrides this category.\n" +
                          "The original will be hidden until you delete the override.",
            OnClick = HandleOverride,
        };
        _overrideButton.AttachNode(this);
    }

    public void SetWrapper(CategoryWrapper? wrapper)
    {
        _wrapper = wrapper;
        if (wrapper is null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        _headerLabel.String = wrapper.GetLabel();
        _kindLabel.String = wrapper.GetSubLabel();
        _overrideButton.IsVisible = wrapper.Kind == CategoryWrapperKind.GameCategory;
        _overrideButton.Reset();
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _headerLabel.Width = Width;
        _kindLabel.Width = Width;
        _explanationLabel.Width = Width;
    }

    private void HandleOverride()
    {
        if (_wrapper?.Kind == CategoryWrapperKind.GameCategory && _wrapper.GameCategoryId is uint id)
            OnOverrideRequested?.Invoke(id);
    }
}
