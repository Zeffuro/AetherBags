using System;
using System.Numerics;
using AetherBags.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryGeneralSettingsEditorNode : SimpleComponentNode
{
    public Action? OnSettingsChanged { get; set; }

    private readonly ScrollingAreaNode<VerticalListNode> _scrollingArea;
    private readonly TextNode _headerLabel;
    private readonly CheckboxNode _showBuiltInCheckbox;
    private readonly CheckboxNode _showExternalCheckbox;
    private readonly CheckboxNode _showGameCheckbox;

    private bool _isRefreshing;

    public CategoryGeneralSettingsEditorNode()
    {
        _scrollingArea = new ScrollingAreaNode<VerticalListNode>
        {
            AutoHideScrollBar = true,
            ContentHeight = 100f,
        };
        _scrollingArea.AttachNode(this);

        var list = _scrollingArea.ContentAreaNode;
        list.FitContents = true;
        list.ItemSpacing = 6.0f;

        _headerLabel = new TextNode
        {
            Size = new Vector2(400, 28),
            FontSize = 18,
            FontType = FontType.MiedingerMed,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(7),
            TextFlags = TextFlags.Edge,
            String = "General Settings",
        };
        list.AddNode(_headerLabel);

        list.AddNode(new ResNode { Height = 4 });

        _showBuiltInCheckbox = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            String = "Show built-in sources",
            OnClick = isChecked =>
            {
                if (_isRefreshing) return;
                System.Config.Categories.ShowBuiltInSourcesInConfig = isChecked;
                Util.SaveConfig(System.Config);
                OnSettingsChanged?.Invoke();
            },
        };
        list.AddNode(_showBuiltInCheckbox);

        _showExternalCheckbox = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            String = "Show external sources",
            OnClick = isChecked =>
            {
                if (_isRefreshing) return;
                System.Config.Categories.ShowExternalSourcesInConfig = isChecked;
                Util.SaveConfig(System.Config);
                OnSettingsChanged?.Invoke();
            },
        };
        list.AddNode(_showExternalCheckbox);

        _showGameCheckbox = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            String = "Show game categories",
            OnClick = isChecked =>
            {
                if (_isRefreshing) return;
                System.Config.Categories.ShowGameCategoriesInConfig = isChecked;
                Util.SaveConfig(System.Config);
                OnSettingsChanged?.Invoke();
            },
        };
        list.AddNode(_showGameCheckbox);
    }

    public void Refresh()
    {
        var categories = System.Config.Categories;
        _isRefreshing = true;
        try
        {
            _showBuiltInCheckbox.IsChecked = categories.ShowBuiltInSourcesInConfig;
            _showExternalCheckbox.IsChecked = categories.ShowExternalSourcesInConfig;
            _showGameCheckbox.IsChecked = categories.ShowGameCategoriesInConfig;
        }
        finally
        {
            _isRefreshing = false;
        }

        _scrollingArea.ContentAreaNode.RecalculateLayout();
        _scrollingArea.ContentHeight = _scrollingArea.ContentAreaNode.Height;
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _scrollingArea.Position = Vector2.Zero;
        _scrollingArea.Size = Size;
        _scrollingArea.ContentAreaNode.RecalculateLayout();
        _scrollingArea.ContentHeight = _scrollingArea.ContentAreaNode.Height;
    }
}
