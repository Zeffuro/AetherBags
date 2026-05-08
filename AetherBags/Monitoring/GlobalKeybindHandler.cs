using System;
using AetherBags.Addons;
using AetherBags.Addons.Config;
using AetherBags.Configuration;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AetherBags.Monitoring;

public sealed unsafe class GlobalKeybindHandler : IDisposable
{
    private bool _searchFocusWasPressed;
    private bool _openBagsWasPressed;
    private bool _openSaddlebagsWasPressed;

    public GlobalKeybindHandler()
    {
        Services.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (ShouldSuppressKeybinds())
        {
            ResetPressedState();
            return;
        }

        KeybindSettings config = System.Config.Keybinds;

        HandleKeybind(config.SearchFocusEnabled, config.SearchFocusKeybind, FocusCurrentSearch, ref _searchFocusWasPressed);
        HandleKeybind(config.OpenBagsEnabled, config.OpenBagsKeybind, OpenInventoryWithSearchFocus, ref _openBagsWasPressed);
        HandleKeybind(config.OpenSaddlebagsEnabled, config.OpenSaddlebagsKeybind, OpenSaddlebagsWithSearchFocus, ref _openSaddlebagsWasPressed);
    }

    private delegate void KeybindCallback(ref bool isHandled);

    private static void HandleKeybind(bool isEnabled, Keybind keybind, KeybindCallback callback, ref bool wasPressed)
    {
        bool isPressed = isEnabled && keybind.IsPressed(exactModifiers: true);

        if (isPressed && !wasPressed)
        {
            var isHandled = false;
            callback(ref isHandled);

            if (isHandled)
            {
                keybind.Reset();
            }
        }

        wasPressed = isPressed;
    }

    private static bool ShouldSuppressKeybinds()
    {
        if (!Services.ClientState.IsLoggedIn) return true;
        if (Services.Condition.Any(ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51)) return true;
        if (RaptureAtkModule.Instance()->IsTextInputActive()) return true;

        return KeybindConfigAddon.IsCapturingKeybind;
    }

    private static void FocusCurrentSearch(ref bool isHandled)
    {
        if (System.AddonInventoryWindow.TryFocusSearch()
            || System.AddonSaddleBagWindow.TryFocusSearch()
            || System.AddonRetainerWindow.TryFocusSearch())
        {
            isHandled = true;
        }
    }

    private static void OpenInventoryWithSearchFocus(ref bool isHandled)
    {
        OpenWithSearchFocus(System.AddonInventoryWindow);
        isHandled = true;
    }

    private static void OpenSaddlebagsWithSearchFocus(ref bool isHandled)
    {
        OpenWithSearchFocus(System.AddonSaddleBagWindow);
        isHandled = true;
    }

    private static void OpenWithSearchFocus(InventoryAddonBase window)
    {
        if (!window.IsOpen)
        {
            window.Open();
        }

        window.FocusSearch();
    }

    private void ResetPressedState()
    {
        _searchFocusWasPressed = false;
        _openBagsWasPressed = false;
        _openSaddlebagsWasPressed = false;
    }

    public void Dispose()
    {
        Services.Framework.Update -= OnFrameworkUpdate;
    }
}

