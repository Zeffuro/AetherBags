using System;

namespace AetherBags.Hooks;

/// <summary>
/// Manages hooks related to inventory operations.
/// </summary>
public sealed partial class InventoryHooks : IDisposable
{
    public InventoryHooks()
    {
        InitializeDebugHooks();
    }

    partial void InitializeDebugHooks();
    partial void DisposeDebugHooks();

    public void Dispose()
    {
        DisposeDebugHooks();
    }
}