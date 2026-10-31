using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Helpers;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Configuration.Category;
using AetherBags.Nodes.Configuration.Currency;
using AetherBags.Nodes.Configuration.General;
using AetherBags.Nodes.Configuration.Keybind;
using AetherBags.Nodes.Configuration.Layout;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public class AddonConfigurationWindow : NativeAddon
{
    private CircleButtonNode? _changelogButtonNode;
    private TabBarNode? _tabBarNode;
    private TabBarNode? _layoutTabBarNode;

    private GeneralScrollingAreaNode? _generalScrollingAreaNode;
    private LayoutScrollingAreaNode? _layoutScrollingAreaNode;
    private CategoryScrollingAreaNode? _categoryScrollingAreaNode;
    private CurrencyScrollingAreaNode? _currencyScrollingAreaNode;
    private KeybindScrollingAreaNode? _keybindScrollingAreaNode;

    private readonly List<NodeBase> _tabContent = new();

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        if (WindowNode is not WindowNode windowNode) return;

        _changelogButtonNode = new CircleButtonNode
        {
            Position = new Vector2(Size.X - 66f, 6f),
            Size = new Vector2(28f),
            Icon = CircleButtonIcon.QuestionMark,
            OnClick = () => { if (!Plugin.IsUnloading) System.AddonChangelogWindow.Toggle(); },
        };
        _changelogButtonNode.AttachNode(windowNode.HeaderContainerNode);

        var tabContentY = ContentStartPosition.Y + 40;
        var tabContentHeight = ContentSize.Y - 40;

        _tabContent.Clear();

        _tabBarNode = new TabBarNode
        {
            Position = ContentStartPosition,
            Size = ContentSize with { Y = 24 },
            IsVisible = true
        };
        _tabBarNode.AttachNode(this);

        _generalScrollingAreaNode = new GeneralScrollingAreaNode
        {
            Position = ContentStartPosition with { Y = tabContentY },
            Size = ContentSize with { Y = tabContentHeight },
            IsVisible = true,
        };
        _generalScrollingAreaNode.AttachNode(this);

        _layoutScrollingAreaNode = new LayoutScrollingAreaNode
        {
            Position = ContentStartPosition with { Y = ContentStartPosition.Y + 62 },
            Size = ContentSize with { Y = ContentSize.Y - 62 },
            IsVisible = false,
        };
        _layoutScrollingAreaNode.AttachNode(this);

        _categoryScrollingAreaNode = new CategoryScrollingAreaNode
        {
            Position = ContentStartPosition with { Y = tabContentY },
            Size = ContentSize with { Y = tabContentHeight },
            IsVisible = false,
        };
        _categoryScrollingAreaNode.AttachNode(this);

        _currencyScrollingAreaNode = new CurrencyScrollingAreaNode
        {
            Position = ContentStartPosition with { Y = tabContentY },
            Size = ContentSize with { Y = tabContentHeight },
            IsVisible = false,
        };
        _currencyScrollingAreaNode.AttachNode(this);

        _keybindScrollingAreaNode = new KeybindScrollingAreaNode
        {
            Position = ContentStartPosition with { Y = tabContentY },
            Size = ContentSize with { Y = tabContentHeight },
            IsVisible = false,
        };
        _keybindScrollingAreaNode.AttachNode(this);

        _layoutTabBarNode = new TabBarNode
        {
            Position = ContentStartPosition with { Y = ContentStartPosition.Y + 26 },
            Size = ContentSize with { Y = 24 },
            IsVisible = false,
        };
        _layoutTabBarNode.AddTab("Global", () => _layoutScrollingAreaNode.SelectSource(0));
        _layoutTabBarNode.AddTab("Inventory", () => _layoutScrollingAreaNode.SelectSource(1));
        _layoutTabBarNode.AddTab("Saddlebags", () => _layoutScrollingAreaNode.SelectSource(2));
        _layoutTabBarNode.AddTab("Retainers", () => _layoutScrollingAreaNode.SelectSource(3));
        _layoutTabBarNode.AttachNode(this);

        _tabContent.Add(_generalScrollingAreaNode);
        _tabContent.Add(_layoutScrollingAreaNode);
        _tabContent.Add(_categoryScrollingAreaNode);
        _tabContent.Add(_currencyScrollingAreaNode);
        _tabContent.Add(_keybindScrollingAreaNode);

        _tabBarNode.AddTab("General", () => SwitchTab(0));
        _tabBarNode.AddTab("Layout", () => SwitchTab(1));
        _tabBarNode.AddTab("Categories", () => SwitchTab(2));
        _tabBarNode.AddTab("Currency", () => SwitchTab(3));
        _tabBarNode.AddTab("Keybinds", () => SwitchTab(4));

        base.OnSetup(addon, atkValueSpan);
    }

    private void SwitchTab(int index)
    {
        for (var i = 0; i < _tabContent.Count; i++)
            _tabContent[i].IsVisible = i == index;
        if (_layoutTabBarNode != null) _layoutTabBarNode.IsVisible = index == 1;
    }

    protected override unsafe void OnHide(AtkUnitBase* addon)
    {
        ColorInputRow.CancelActiveSession(addon);
        System.CategoryConfigurationWindow?.Close();
        base.OnHide(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        Util.SaveConfig(System.Config);

        _tabContent.Clear();
        _tabBarNode = null;
        _layoutTabBarNode = null;
        _generalScrollingAreaNode = null;
        _layoutScrollingAreaNode = null;
        _categoryScrollingAreaNode = null;
        _currencyScrollingAreaNode = null;
        _keybindScrollingAreaNode = null;
        _changelogButtonNode = null;

        base.OnFinalize(addon);
    }
}
