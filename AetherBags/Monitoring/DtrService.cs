using System;
using AetherBags.Configuration;
using AetherBags.Inventory.Items;
using AetherBags.Tags;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags.Monitoring;

public sealed unsafe class DtrService : IDisposable
{
    private const string EntryTitle = "AetherBags";

    private IDtrBarEntry? _dtrEntry;
    private DateTime _lastUpdate = DateTime.MinValue;

    private DtrSettings Settings => System.Config.Dtr;

    public DtrService()
    {
        Services.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if ((DateTime.Now - _lastUpdate).TotalMilliseconds < 250) return;
        _lastUpdate = DateTime.Now;

        UpdateBar();
    }

    public void UpdateBar()
    {
        if (!Settings.Enabled)
        {
            RemoveEntry();
            return;
        }

        EnsureEntry();
        if (_dtrEntry == null) return;

        if (!Services.ClientState.IsLoggedIn)
        {
            _dtrEntry.Shown = false;
            return;
        }

        var context = DtrTagContext.Create();
        var format = string.IsNullOrWhiteSpace(Settings.FormatString) ? DtrSettings.DefaultFormatString : Settings.FormatString;
        _dtrEntry.Text = new SeStringBuilder().AddText(DtrTagEngine.Process(format, context)).Build();
        _dtrEntry.Tooltip = BuildTooltip(context);
        _dtrEntry.Shown = true;
    }

    private void EnsureEntry()
    {
        if (_dtrEntry != null) return;

        _dtrEntry = Services.DtrBar.Get(EntryTitle, new SeStringBuilder().AddText(EntryTitle).Build());
        _dtrEntry.Shown = true;
        _dtrEntry.OnClick += OnDtrClick;
    }

    private static SeString BuildTooltip(DtrTagContext context)
    {
        var builder = new SeStringBuilder();
        builder.AddUiForeground(EntryTitle, 540);
        AppendStats(builder, "Main", context.Main);
        AppendStats(builder, "Saddle", context.Saddle);
        builder.AddUiForeground("\n\nLeft click: toggle bags\nRight click: toggle saddlebags", 48);
        return builder.Build();
    }

    private static void AppendStats(SeStringBuilder builder, string label, InventoryStats stats)
    {
        builder.AddText($"\n{label}: {stats.UsedSlots:N0}/{stats.TotalSlots:N0} slots ({stats.UsagePercent:N0}%)");
    }

    private static void OnDtrClick(DtrInteractionEvent interaction)
    {
        switch (interaction.ClickType)
        {
            case MouseClickType.Left:
                System.AddonInventoryWindow.Toggle();
                break;
            case MouseClickType.Right:
                ShowAgent(AgentId.InventoryBuddy);
                break;
        }
    }

    private static void ShowAgent(AgentId agentId)
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(agentId);
        agent->Show();
    }

    private void RemoveEntry()
    {
        if (_dtrEntry == null) return;

        _dtrEntry.OnClick -= OnDtrClick;
        _dtrEntry.Remove();
        _dtrEntry = null;
    }

    public void Dispose()
    {
        Services.Framework.Update -= OnFrameworkUpdate;
        RemoveEntry();
    }
}
