using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace AetherBags.IPC;

public class IPCService : IDisposable
{
    public AllaganToolsIPC AllaganTools { get; }
    public WotsItIPC WotsIt { get; }
    // Future: public BiSBuddyIPC BiSBuddy { get; }

    public IPCService()
    {
        AllaganTools = new AllaganToolsIPC();
        WotsIt = new WotsItIPC();
    }

    public void Dispose()
    {
        AllaganTools.Dispose();
    }
}