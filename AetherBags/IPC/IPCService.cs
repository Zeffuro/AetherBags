using System;
using AetherBags.Configuration;

namespace AetherBags.IPC;

public class IPCService : IDisposable
{
    public AllaganToolsIPC AllaganTools { get; } = new();
    public WotsItIPC WotsIt { get; } = new();
    public BisBuddyIPC BisBuddy { get; } = new();
    public TestExternalSource TestSource { get; } = new();
    public CrystalExternalSource Crystals { get; } = new();
    public KeyItemExternalSource KeyItems { get; } = new();

    public void RefreshExternalSources()
    {
        var config = System.Config?.Categories;
        if (config == null) return;

        bool categoriesEnabled = config.CategoriesEnabled;

        bool allaganShouldBeActive = categoriesEnabled &&
                                     config.AllaganToolsCategoriesEnabled &&
                                     config.AllaganToolsFilterMode == PluginFilterMode.Categorize;

        if (allaganShouldBeActive)
            AllaganTools.EnableExternalCategorySupport();
        else
            AllaganTools.DisableExternalCategorySupport();

        bool bisBuddyShouldBeActive = categoriesEnabled &&
                                       config.BisBuddyEnabled &&
                                       config.BisBuddyMode == PluginFilterMode.Categorize;

        if (bisBuddyShouldBeActive)
            BisBuddy.EnableExternalCategorySupport();
        else
            BisBuddy.DisableExternalCategorySupport();

        bool crystalsShouldBeActive = categoriesEnabled && config.CrystalsEnabled;
        if (crystalsShouldBeActive) Crystals.Enable(); else Crystals.Disable();

        bool keyItemsShouldBeActive = categoriesEnabled && config.KeyItemsEnabled;
        if (keyItemsShouldBeActive) KeyItems.Enable(); else KeyItems.Disable();
    }

    public void ToggleTestSource()
    {
        if (TestSource.IsReady)
            TestSource.Disable();
        else
            TestSource.Enable();
    }

    public void Dispose()
    {
        AllaganTools.Dispose();
        WotsIt.Dispose();
        BisBuddy.Dispose();
        TestSource.Dispose();
        Crystals.Dispose();
        KeyItems.Dispose();
    }
}