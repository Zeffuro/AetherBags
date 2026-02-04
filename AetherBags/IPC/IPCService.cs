using System;
using AetherBags.Configuration;

namespace AetherBags.IPC;

public class IPCService : IDisposable
{
    public AllaganToolsIPC AllaganTools { get; } = new();
    public WotsItIPC WotsIt { get; } = new();
    public BisBuddyIPC BisBuddy { get; } = new();

    private bool _unifiedEnabled;

    public void UpdateUnifiedCategorySupport(bool enabled)
    {
        _unifiedEnabled = enabled;
        RefreshExternalSources();
    }

    public void RefreshExternalSources()
    {
        var config = System.Config?.Categories;
        if (config == null) return;

        bool categoriesEnabled = config.CategoriesEnabled;

        bool allaganShouldBeActive = _unifiedEnabled &&
                                      categoriesEnabled &&
                                      config.AllaganToolsCategoriesEnabled &&
                                      config.AllaganToolsFilterMode == PluginFilterMode.Categorize;

        if (allaganShouldBeActive)
            AllaganTools.EnableExternalCategorySupport();
        else
            AllaganTools.DisableExternalCategorySupport();

        bool bisBuddyShouldBeActive = _unifiedEnabled &&
                                       categoriesEnabled &&
                                       config.BisBuddyEnabled &&
                                       config.BisBuddyMode == PluginFilterMode.Categorize;

        if (bisBuddyShouldBeActive)
            BisBuddy.EnableExternalCategorySupport();
        else
            BisBuddy.DisableExternalCategorySupport();
    }

    public void Dispose()
    {
        AllaganTools.Dispose();
        WotsIt.Dispose();
        BisBuddy.Dispose();
    }
}