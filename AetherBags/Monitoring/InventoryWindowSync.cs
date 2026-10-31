using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Addons;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Monitoring;

internal sealed unsafe class InventoryWindowSync : IDisposable
{
    private static readonly string[] ChildAddonNames =
    [
        "InventoryGrid", "InventoryGridCrystal", "InventoryGrid0", "InventoryGrid1",
        "InventoryGrid0E", "InventoryGrid1E", "InventoryGrid2E", "InventoryGrid3E",
        "InventoryEventGrid0", "InventoryEventGrid1", "InventoryEventGrid2",
        "InventoryEventGrid0E", "InventoryEventGrid1E", "InventoryEventGrid2E", "InventoryCrystalGrid", "InventoryEventGrid",
        "RetainerGrid", "RetainerGrid0",
        "RetainerGrid1", "RetainerGrid2", "RetainerGrid3", "RetainerGrid4", "RetainerGrid5",
        "RetainerGridCrystal", "RetainerCrystalGrid"
    ];

    private readonly WindowState[] _windows;
    private bool _isDisposed;

    public InventoryWindowSync()
    {
        _windows =
        [
            new(System.AddonInventoryWindow, ["Inventory", "InventoryLarge", "InventoryExpansion", "InventoryEvent"],
                () => System.Config.General.OpenWithGameInventory, () => System.Config.General.HideGameInventory),
            new(System.AddonSaddleBagWindow, ["InventoryBuddy"],
                () => System.Config.General.OpenSaddleBagsWithGameInventory, () => System.Config.General.HideGameSaddleBags),
            new(System.AddonRetainerWindow, ["InventoryRetainer", "InventoryRetainerLarge"],
                () => System.Config.General.OpenRetainerWithGameInventory, () => System.Config.General.HideGameRetainer)
        ];

        foreach (var state in _windows)
        {
            state.HideHandler = () => OnAddonHide(state);
            state.Window.Hiding += state.HideHandler;
        }

        var addonNames = _windows.SelectMany(state => state.Names).ToArray();
        var lifecycle = IAddonLifecycle.Get();
        lifecycle.RegisterListener(AddonEvent.PostShow, addonNames, OnLifecycle);
        lifecycle.RegisterListener(AddonEvent.PostHide, addonNames, OnLifecycle);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, addonNames, OnLifecycle);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, addonNames, OnLifecycle);
        lifecycle.RegisterListener(AddonEvent.PreRefresh, addonNames, OnLifecycle);
        lifecycle.RegisterListener(AddonEvent.PostRefresh, addonNames, OnLifecycle);
        lifecycle.RegisterListener(AddonEvent.PostShow, ChildAddonNames, OnChildLifecycle);
        lifecycle.RegisterListener(AddonEvent.PostSetup, ChildAddonNames, OnChildLifecycle);
        IFramework.Get().Update += OnFrameworkUpdate;
    }

    private void OnLifecycle(AddonEvent type, AddonArgs args)
    {
        if (_isDisposed || args.Addon.IsNull) return;
        var state = FindPair(args.AddonName);
        if (state == null) return;
        var addon = (AtkUnitBase*)args.Addon.Address;

        if (type == AddonEvent.PreRefresh)
        {
            state.RefreshDepth++;
            if (CanAccessGame() && state.GameWindowOpen && state.GameAddonName == args.AddonName && addon->Id == state.GameAddonId)
                RestoreDefault(state);
            return;
        }
        if (type == AddonEvent.PostRefresh && state.RefreshDepth > 0)
            state.RefreshDepth--;

        if (type is AddonEvent.PostHide or AddonEvent.PreFinalize)
        {
            if (state.GameAddonName == args.AddonName && state.GameAddonId == addon->Id)
                OnGameHide(state);
            return;
        }

        if (type == AddonEvent.PostShow || (addon->IsVisible && !state.GameWindowOpen))
            OnGameShow(state, args.AddonName, addon->Id);

        if (type is AddonEvent.PostShow or AddonEvent.PostUpdate or AddonEvent.PostRefresh
            && state.RefreshDepth == 0 && CanSync(state))
            HideDefault(state);
    }

    private WindowState? FindPair(string name)
    {
        foreach (var state in _windows)
        {
            if (Array.IndexOf(state.Names, name) >= 0) return state;
        }
        return null;
    }

    private void OnChildLifecycle(AddonEvent type, AddonArgs args)
    {
        if (_isDisposed || args.Addon.IsNull) return;
        var addon = (AtkUnitBase*)args.Addon.Address;

        if (type != AddonEvent.PostSetup && !addon->IsReady) return;
        foreach (var state in _windows)
        {
            if (addon->ParentId != state.GameAddonId || state.RefreshDepth != 0 || !state.GameWindowOpen
                || !state.HideDefault() || !CanSync(state)) continue;
            HideAddon(state, addon);
            return;
        }
    }

    private static void OnGameShow(WindowState state, string name, ushort id)
    {
        state.GameAddonName = name;
        state.GameAddonId = id;
        state.GameWindowOpen = true;
        state.CloseRequested = false;
    }

    private static void OnGameHide(WindowState state)
    {
        if (!state.GameWindowOpen) return;
        state.GameWindowOpen = false;
        state.CloseRequested = false;
        state.HiddenAddons.Clear();
        state.DefaultHidden = false;
        state.CloseTargetId = state.Window.AddonId;
    }

    private void OnAddonHide(WindowState state)
    {
        if (_isDisposed || state.ClosingAddonId == state.Window.AddonId || !state.IsSynced || !CanSync(state)) return;
        state.CloseRequested = true;
        state.ClosedAddonId = state.Window.AddonId;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_isDisposed) return;
        foreach (var state in _windows)
        {
            if (state.RefreshDepth > 0) continue;
            if (!state.GameWindowOpen && !state.IsSynced && !state.CloseRequested
                && state.HiddenAddons.Count == 0) continue;
            if (!CanSync(state))
            {
                state.CloseRequested = false;
                state.IsSynced = false;
                if (CanAccessGame()) RestoreDefault(state);
                continue;
            }

            var addon = GetGameAddon(state);
            if (state.GameWindowOpen && addon == null)
                OnGameHide(state);

            if (state.CloseRequested)
            {
                state.CloseRequested = false;
                if (state.GameWindowOpen && addon != null && addon->IsReady
                    && (state.Window.AddonId == 0 || state.Window.AddonId == state.ClosedAddonId))
                {
                    RestoreDefault(state);
                    addon->Hide2();
                    if (CanSync(state)) HideDefault(state);
                    continue;
                }
            }

            if (state.GameWindowOpen)
            {
                state.IsSynced = true;
                if (state.Window.AddonId == 0)
                {
                    state.ClosingAddonId = 0;
                    state.Window.Open();
                }
            }
            else if (state.IsSynced && state.Window.IsOpen)
            {
                if (state.Window.AddonId == state.CloseTargetId)
                {
                    state.ClosingAddonId = state.Window.AddonId;
                    state.Window.Close();
                }
                else
                {
                    state.IsSynced = false;
                }
            }

            if (state.Window.AddonId == 0 && !state.GameWindowOpen)
            {
                state.ClosingAddonId = 0;
                state.IsSynced = false;
            }

            if (state.HideDefault() && state.GameWindowOpen && state.Window.IsOpen)
            {
                if (!state.DefaultHidden) HideDefault(state);
            }
            else
                RestoreDefault(state);
        }
    }

    private static bool CanAccessGame()
        => !IFramework.Get().IsFrameworkUnloading && IClientState.Get().IsLoggedIn
            && !ICondition.Get()[ConditionFlag.BetweenAreas] && !ICondition.Get()[ConditionFlag.BetweenAreas51];

    private static bool CanSync(WindowState state)
        => !Plugin.IsUnloading && CanAccessGame() && state.AutoOpen()
            && !(System.AetherBagsAPI?.API.IsVanillaInventoryBypassActive ?? false);

    private static AtkUnitBase* GetGameAddon(WindowState state)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null || state.GameAddonName == null) return null;
        var addon = manager->GetAddonByName(state.GameAddonName);
        return addon != null && addon->Id == state.GameAddonId ? addon : null;
    }

    private static void HideDefault(WindowState state)
    {
        if (!state.HideDefault() || !state.GameWindowOpen) return;
        var addon = GetGameAddon(state);
        if (addon == null || !addon->IsReady) return;
        HideAddon(state, addon);
        state.DefaultHidden = true;

        var control = GetAddonControl(addon, state.GameAddonName);
        if (control == null) return;
        foreach (var child in control->ChildAddons)
        {
            if (child.Value == null || child.Value->AtkUnitBase == null) continue;
            if (child.Value->AtkUnitBase->ParentId == state.GameAddonId && child.Value->AtkUnitBase->IsReady)
                HideAddon(state, child.Value->AtkUnitBase);
        }
    }

    private static AtkAddonControl* GetAddonControl(AtkUnitBase* addon, string? name) => name switch
    {
        "Inventory" => &((AddonInventory*)addon)->AddonControl,
        "InventoryLarge" => &((AddonInventoryLarge*)addon)->AddonControl,
        "InventoryExpansion" => &((AddonInventoryExpansion*)addon)->AddonControl,
        "InventoryEvent" => &((AddonInventoryEvent*)addon)->AddonControl,
        "InventoryRetainer" => &((AddonInventoryRetainer*)addon)->AddonControl,
        "InventoryRetainerLarge" => &((AddonInventoryRetainerLarge*)addon)->AddonControl,
        _ => null,
    };

    private static void HideAddon(WindowState state, AtkUnitBase* addon)
    {
        if (!addon->IsVisible) return;
        state.HiddenAddons.Add(addon->Id);
        addon->IsVisible = false;
    }

    private static void RestoreDefault(WindowState state)
    {
        if (state.HiddenAddons.Count == 0)
        {
            state.DefaultHidden = false;
            return;
        }
        var manager = RaptureAtkUnitManager.Instance();
        if (manager != null && state.GameWindowOpen && GetGameAddon(state) != null)
        {
            foreach (var id in state.HiddenAddons)
            {
                var addon = manager->GetAddonById(id);
                if (addon != null && addon->IsReady && (id == state.GameAddonId || addon->ParentId == state.GameAddonId))
                    addon->IsVisible = true;
            }
        }
        state.HiddenAddons.Clear();
        state.DefaultHidden = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IFramework.Get().Update -= OnFrameworkUpdate;
        IAddonLifecycle.Get().UnregisterListener(OnLifecycle);
        IAddonLifecycle.Get().UnregisterListener(OnChildLifecycle);
        foreach (var state in _windows)
        {
            state.Window.Hiding -= state.HideHandler;
            state.CloseRequested = false;
            if (CanAccessGame()) RestoreDefault(state);
        }
    }

    private sealed class WindowState(InventoryAddonBase window, string[] names, Func<bool> autoOpen, Func<bool> hideDefault)
    {
        public InventoryAddonBase Window { get; } = window;
        public string[] Names { get; } = names;
        public Func<bool> AutoOpen { get; } = autoOpen;
        public Func<bool> HideDefault { get; } = hideDefault;
        public HashSet<ushort> HiddenAddons { get; } = [];
        public Action HideHandler { get; set; } = null!;
        public string? GameAddonName { get; set; }
        public ushort GameAddonId { get; set; }
        public bool CloseRequested { get; set; }
        public int ClosedAddonId { get; set; }
        public bool GameWindowOpen { get; set; }
        public bool DefaultHidden { get; set; }
        public bool IsSynced { get; set; }
        public int ClosingAddonId { get; set; }
        public int CloseTargetId { get; set; }
        public int RefreshDepth { get; set; }
    }
}
