using System;

namespace AetherBags.IPC;

public class IPCService : IDisposable
{
    public AllaganToolsIPC AllaganTools { get; } = new();
    public WotsItIPC WotsIt { get; } = new();
    public BisBuddyIPC BisBuddy { get; } = new();

    public void Dispose()
    {
        AllaganTools.Dispose();
        WotsIt.Dispose();
        BisBuddy.Dispose();
    }
}