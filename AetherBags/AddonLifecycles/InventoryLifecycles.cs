using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace AetherBags.AddonLifecycles;

public class InventoryLifecycles : IDisposable
{

    public InventoryLifecycles()
    {
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreOpen, ["Inventory", "InventoryLarge", "InventoryExpansion"], HandleInventorySetup);
        Services.Logger.Verbose("InventoryLifecycles initialized");
    }

    private void HandleInventorySetup(AddonEvent type, AddonArgs args)
    {
        Services.Logger.Debug("HandleInventorySetup called");
    }

    public void Dispose()
    {
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreOpen, ["Inventory", "InventoryLarge", "InventoryExpansion"]);
    }
}