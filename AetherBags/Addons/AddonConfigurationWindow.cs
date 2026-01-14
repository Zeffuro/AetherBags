using System.Collections.Generic;
using AetherBags.Nodes.Configuration.Category;
using AetherBags.Nodes.Configuration.Currency;
using AetherBags.Nodes.Configuration.General;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public class AddonConfigurationWindow : NativeAddon
{
    private TabBarNode? _tabBarNode;

    private GeneralScrollingAreaNode? _generalScrollingAreaNode;
    private CategoryScrollingAreaNode? _categoryScrollingAreaNode;
    private CurrencyScrollingAreaNode? _currencyScrollingAreaNode;

    private readonly List<NodeBase> _tabContent = new();

    protected override unsafe void OnSetup(AtkUnitBase* addon)
    {
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

        _tabContent.Add(_generalScrollingAreaNode);
        _tabContent.Add(_categoryScrollingAreaNode);
        _tabContent.Add(_currencyScrollingAreaNode);

        _tabBarNode.AddTab("General", () => SwitchTab(0));
        _tabBarNode.AddTab("Categories", () => SwitchTab(1));
        _tabBarNode.AddTab("Currency", () => SwitchTab(2));

        base.OnSetup(addon);
    }

    private void SwitchTab(int index)
    {
        for (var i = 0; i < _tabContent.Count; i++)
            _tabContent[i].IsVisible = i == index;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        _tabBarNode?.Dispose();
        _tabBarNode = null;
        _generalScrollingAreaNode?.Dispose();
        _generalScrollingAreaNode = null;
        _categoryScrollingAreaNode?.Dispose();
        _categoryScrollingAreaNode = null;
        _currencyScrollingAreaNode?.Dispose();
        _currencyScrollingAreaNode = null;
        base.OnFinalize(addon);
    }
}