using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Layout;
using AetherBags.Inventory.Scanning;
using AetherBags.Inventory.State;
using AetherBags.Monitoring;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract partial class InventoryAddonBase
{
    private readonly InventoryLayoutSession _layoutSession = new();
    private InventoryLayoutOptions? _layoutOptions;
    private EffectiveWindowSizing _layoutSizing;
    private ulong _layoutCharacter;
    private ulong _layoutRetainer;
    private ulong _layoutFingerprint;
    private bool _hasLayoutFingerprint;
    private bool _layoutHolding;
    private bool _layoutFiltered;
    private bool _layoutNeedsSettle;
    private uint _layoutContext;
    private long _layoutChangedAt;
    private float _layoutWidth;
    private bool _layoutSized;
    private TextNode? _layoutWarningNode;
    private InventoryStackMode _layoutStackMode;
    private bool _layoutAggregate;
    private (bool Enabled, bool Largest, bool Stable, int Lookahead) _layoutPacking;
    private static readonly AgentId[] ShopAgents =
    [
        AgentId.Shop, AgentId.InclusionShop, AgentId.CollectablesShop,
        AgentId.ShopExchangeCoin, AgentId.FreeCompanyCreditShop, AgentId.FreeShop, AgentId.MJIDisposeShop,
    ];

    private InventoryLayoutSettings WindowLayoutSettings
    {
        get => InventoryState.SourceType switch
        {
            InventorySourceType.SaddleBag or InventorySourceType.AllSaddleBags or InventorySourceType.PremiumSaddleBag => System.Config.General.SaddleBagLayout,
            InventorySourceType.Retainer => System.Config.General.RetainerLayout,
            _ => System.Config.General.InventoryLayout,
        };
        set
        {
            switch (InventoryState.SourceType)
            {
                case InventorySourceType.SaddleBag:
                case InventorySourceType.AllSaddleBags:
                case InventorySourceType.PremiumSaddleBag: System.Config.General.SaddleBagLayout = value; break;
                case InventorySourceType.Retainer: System.Config.General.RetainerLayout = value; break;
                default: System.Config.General.InventoryLayout = value; break;
            }
        }
    }

    internal InventoryLayoutSettings EffectiveLayoutSettings
        => WindowLayoutSettings.UseGlobal ? System.Config.General.Layout : WindowLayoutSettings;

    private void PrepareLayoutSizing()
    {
        var sizing = GetEffectiveWindowSizing();
        if (sizing.Mode == InventoryWindowSizingMode.Fixed
            && (Math.Abs(Size.X - sizing.FixedWidth) > 0.5f || Math.Abs(Size.Y - sizing.FixedHeight) > 0.5f))
        {
            ClearLayout();
            ResizeWindow(sizing.FixedWidth, sizing.FixedHeight, true);
        }
    }

    private IReadOnlyList<InventoryLayoutBlock> PrepareLayout(IReadOnlyList<CategorizedInventory> categories, int columns, bool filtered)
    {
        var settings = EffectiveLayoutSettings;
        var options = settings.Options;
        var geometryOptions = options with { ShowEmptySlots = false, Delay = 0 };
        var sizing = GetEffectiveWindowSizing();
        ulong character = IPlayerState.Get().ContentId;
        ulong retainer = InventoryState.SourceType == InventorySourceType.Retainer ? RetainerState.CurrentRetainerId : 0;
        var general = System.Config.General;
        uint context = GetLayoutContext();
        var packing = (general.CompactPackingEnabled, general.CompactPreferLargestFit, general.CompactStableInsert, general.CompactLookahead);
        if (_layoutFiltered != filtered) ResetLayoutScroll();
        if (_layoutOptions != geometryOptions || _layoutSizing != sizing || _layoutCharacter != character
            || _layoutRetainer != retainer || _layoutFiltered != filtered || _layoutStackMode != general.StackMode
            || _layoutAggregate != general.AggregateUnstackableItems
            || _layoutPacking != packing
            || _layoutWidth > 0 && Math.Abs(_layoutWidth - CategoriesNode.Width) > 0.5f
            || options.Updates == InventoryLayoutUpdates.Live && _layoutContext != context)
        {
            ClearLayout();
            _hasLayoutFingerprint = false;
        }
        _layoutOptions = geometryOptions;
        _layoutSizing = sizing;
        _layoutCharacter = character;
        _layoutRetainer = retainer;
        _layoutFiltered = filtered;
        _layoutStackMode = general.StackMode;
        _layoutAggregate = general.AggregateUnstackableItems;
        _layoutPacking = packing;
        _layoutContext = context;

        ulong fingerprint = GetLayoutFingerprint(categories);
        if (!_hasLayoutFingerprint || fingerprint != _layoutFingerprint)
        {
            _layoutFingerprint = fingerprint;
            _hasLayoutFingerprint = true;
            _layoutChangedAt = Environment.TickCount64;
            _layoutNeedsSettle = true;
        }
        _layoutHolding = !filtered && (options.Updates != InventoryLayoutUpdates.Live || HoldsContext(options.Contexts, context));
        CategoriesNode.SeparateRows = options.SeparateRows;
        CategoriesNode.HoldPositions = _layoutHolding;
        return _layoutSession.Update(categories, columns, _layoutHolding, options.Retention == InventoryLayoutRetention.Items);
    }

    private static unsafe uint GetLayoutContext()
    {
        var inventory = AgentInventory.Instance();
        uint context = inventory == null ? 0 : inventory->OpenTitleId;
        if (context is (uint)InventoryNotificationType.Sell or (uint)InventoryNotificationType.Markets) return context;
        var module = AgentModule.Instance();
        if (module == null) return context;
        foreach (var id in ShopAgents)
        {
            var agent = module->GetAgentByInternalId(id);
            if (agent != null && agent->IsAgentActive()) return (uint)InventoryNotificationType.Sell;
        }
        return context;
    }

    private static bool HoldsContext(InventoryLayoutContexts contexts, uint context)
    {
        var group = (InventoryNotificationType)context switch
        {
            InventoryNotificationType.None => InventoryLayoutContexts.None,
            InventoryNotificationType.Sell or InventoryNotificationType.Markets => InventoryLayoutContexts.Shops,
            InventoryNotificationType.Trade or InventoryNotificationType.Trade2 or InventoryNotificationType.Trade3
                or InventoryNotificationType.Trade4 or InventoryNotificationType.Letters => InventoryLayoutContexts.TradeAndMail,
            InventoryNotificationType.Retainer or InventoryNotificationType.RetainerEquip => InventoryLayoutContexts.Retainers,
            InventoryNotificationType.SaddleBag => InventoryLayoutContexts.Saddlebags,
            _ => InventoryLayoutContexts.Other,
        };
        return (contexts & group) != 0;
    }

    private static ulong GetLayoutFingerprint(IReadOnlyList<CategorizedInventory> categories)
    {
        ulong hash = 14695981039346656037;
        foreach (var category in categories)
        {
            hash = (hash ^ category.Key) * 1099511628211;
            foreach (var item in category.Items)
            {
                hash = (hash ^ item.Key) * 1099511628211;
                hash = (hash ^ item.Item.ItemId) * 1099511628211;
                hash = (hash ^ (uint)item.Item.Flags) * 1099511628211;
                hash = (hash ^ (uint)item.ItemCount) * 1099511628211;
                hash = (hash ^ (uint)item.Item.Quantity) * 1099511628211;
                hash = (hash ^ (uint)item.Item.Container) * 1099511628211;
                hash = (hash ^ (uint)item.Item.Slot) * 1099511628211;
                hash = (hash ^ (item.IsSlotBlocked ? 1UL : 0UL)) * 1099511628211;
            }
        }
        return hash;
    }

    private void CompleteLayout()
    {
        _layoutWidth = CategoriesNode.Width;
        if (_layoutHolding) _layoutSized = true;
    }

    private bool KeepLayoutWindowSize()
    {
        if (!_layoutHolding || !_layoutSized) return false;
        LayoutContent();
        CategoriesNode.RecalculateLayout();
        UpdateScrollParameters();
        return true;
    }

    private void UpdateLayoutIdle()
    {
        if (!_layoutHolding || !_layoutNeedsSettle || _layoutFiltered || _isRefreshing || DragDropState.IsDragging) return;
        var settings = EffectiveLayoutSettings;
        if (settings.Updates != InventoryLayoutUpdates.AfterIdle || HoldsContext(settings.Contexts, GetLayoutContext())
            || Environment.TickCount64 - _layoutChangedAt < settings.SettleDelayMilliseconds) return;
        RearrangeLayout();
        _layoutNeedsSettle = false;
    }

    private void ClearLayout()
    {
        _layoutSession.Reset();
        _layoutHolding = false;
        _layoutSized = false;
        if (CategoriesNode == null) return;
        CategoriesNode.HoldPositions = false;
        CategoriesNode.ResetHeldPositions();
    }

    private void UpdateLayoutWarning()
    {
        bool full = ScrollableCategories.IsContentLimited;
        if (full && _layoutWarningNode == null)
        {
            _layoutWarningNode = new TextNode
            {
                FontSize = 12,
                String = "Layout full. Use Layout > Rearrange now to reach new items.",
                TextTooltip = "Rearrange now or reopen the window to clear reserved spaces.",
                TextColor = ColorHelper.GetColor(17),
            };
            _layoutWarningNode.AttachNode(this);
        }
        if (_layoutWarningNode != null)
        {
            _layoutWarningNode.Position = new Vector2(ContentStartPosition.X, ContentStartPosition.Y + ContentSize.Y - FooterHeight);
            _layoutWarningNode.Size = new Vector2(Math.Max(0, ContentSize.X - 100), 24);
            _layoutWarningNode.IsVisible = full;
        }
        if (HasFooter) FooterNode.IsVisible = !full;
    }

    private void ResetLayoutScroll()
    {
        if (ScrollableCategories == null) return;
        ScrollableCategories.ScrollBarNode.ScrollPosition = 0;
        OnScrollValueChanged(0);
    }

    internal void RearrangeLayout()
    {
        ClearLayout();
        ResetLayoutScroll();
        if (IsOpen && IsSetupComplete) RefreshCategoriesCore(true);
    }

    internal void ResetLayoutForConfig()
    {
        if (!IsOpen || !IsSetupComplete) return;
        ClearLayout();
        ResetLayoutScroll();
    }

    internal void SetLayoutUpdates(InventoryLayoutUpdates? updates)
    {
        if (updates == null) WindowLayoutSettings.UseGlobal = true;
        else
        {
            var settings = EffectiveLayoutSettings.Copy();
            settings.UseGlobal = false;
            settings.Updates = updates.Value;
            if (updates == InventoryLayoutUpdates.Live) settings.Contexts = InventoryLayoutContexts.None;
            WindowLayoutSettings = settings;
        }
        Util.SaveConfig(System.Config);
        RearrangeLayout();
    }
}
