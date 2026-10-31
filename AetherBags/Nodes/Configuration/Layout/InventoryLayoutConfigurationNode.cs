using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Nodes.Layout;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Layout;

internal sealed class InventoryLayoutConfigurationNode : TabbedVerticalListNode
{
    private readonly Action? _onLayoutChanged;
    private readonly TabbedVerticalListNode _form;
    private readonly CheckboxNode _useGlobal;
    private readonly EnumDropDownNode<InventoryLayoutUpdates> _updates;
    private readonly EnumDropDownNode<InventoryLayoutRetention> _retention;
    private readonly NumericInputNode _idleDelay;
    private readonly CheckboxNode[] _contexts;
    private readonly CheckboxNode _showEmptySlots;
    private readonly CheckboxNode _separateRows;
    private int _selectedSource;
    private bool _syncing;

    private static readonly (string Label, InventoryLayoutContexts Flag)[] ContextOptions =
    [
        ("Shops", InventoryLayoutContexts.Shops),
        ("Trade and mail", InventoryLayoutContexts.TradeAndMail),
        ("Retainers", InventoryLayoutContexts.Retainers),
        ("Saddlebags", InventoryLayoutContexts.Saddlebags),
        ("Other item exchanges", InventoryLayoutContexts.Other),
    ];

    public InventoryLayoutConfigurationNode(Action? onLayoutChanged = null)
    {
        _onLayoutChanged = onLayoutChanged;
        Width = 560;
        FitWidth = true;
        AddNode(new CategoryTextNode { Height = 18, String = "Layout Stability" });
        _form = new TabbedVerticalListNode { FitWidth = true, ItemSpacing = 4 };
        AddTab(1);
        AddNode(_form);
        SubtractTab(1);
        _useGlobal = new CheckboxNode
        {
            Height = 18,
            String = "Use global settings",
            OnClick = SetUseGlobal,
        };
        _form.AddNode(_useGlobal);

        _updates = AddEnum<InventoryLayoutUpdates>("Update layout",
            "Live rearranges immediately outside the interactions below.\nAfter idle waits until changes settle. When reopened keeps the arrangement until you reopen the bag.",
            value => Edit(settings => settings.Updates = value));

        var idleRow = new ResNode { Height = 28 };
        new LabelTextNode
        {
            Position = new Vector2(0, 4),
            Size = new Vector2(250, 20),
            String = "Idle delay (ms)",
        }.AttachNode(idleRow);
        _idleDelay = new NumericInputNode
        {
            Position = new Vector2(250, 0),
            Size = new Vector2(112, 28),
            Min = 250,
            Max = 10000,
            OnValueUpdate = value => Edit(settings => settings.SettleDelayMilliseconds = Math.Clamp(value, 250, 10000)),
        };
        _idleDelay.AttachNode(idleRow);
        _form.AddNode(idleRow);

        _retention = AddEnum<InventoryLayoutRetention>("Keep in place",
            "Keep category space lets items move within each category.\nKeep item positions leaves gaps so remaining items stay under your pointer.",
            value => Edit(settings => settings.Retention = value));
        _form.AddNode(new ResNode { Height = 6 });
        _form.AddNode(new LabelTextNode { Height = 20, String = "Hold until these interactions close:" });
        _form.AddTab(1);
        _contexts = new CheckboxNode[ContextOptions.Length];
        for (int index = 0; index < ContextOptions.Length; index++)
        {
            var option = ContextOptions[index];
            _contexts[index] = AddCheckbox(option.Label, string.Empty, value =>
                Edit(settings => settings.Contexts = value ? settings.Contexts | option.Flag : settings.Contexts & ~option.Flag));
        }
        _form.SubtractTab(1);
        _form.AddNode(new ResNode { Height = 6 });
        _showEmptySlots = AddCheckbox("Show reserved empty slots",
            "Marks the gaps left by removed items while their positions are held.",
            value => Edit(settings => settings.ShowEmptySlots = value));
        _separateRows = AddCheckbox("Start each category on a new row", string.Empty,
            value => Edit(settings => settings.SeparateCategoryRows = value));
        SyncControls();
    }

    private InventoryLayoutSettings Settings => _selectedSource switch
    {
        1 => System.Config.General.InventoryLayout,
        2 => System.Config.General.SaddleBagLayout,
        3 => System.Config.General.RetainerLayout,
        _ => System.Config.General.Layout,
    };

    private bool IsInherited => _selectedSource != 0 && Settings.UseGlobal;

    private EnumDropDownNode<T> AddEnum<T>(string label, string tooltip, Action<T> changed) where T : struct, Enum
    {
        var row = new ResNode { Height = 24 };
        new LabelTextNode
        {
            Position = new Vector2(0, 2),
            Size = new Vector2(250, 20),
            String = label,
            TextTooltip = tooltip,
        }.AttachNode(row);
        var dropdown = new EnumDropDownNode<T>
        {
            Position = new Vector2(250, 0),
            Size = new Vector2(250, 24),
            Options = Enum.GetValues<T>().ToList(),
            TextTooltip = tooltip,
            OnOptionSelected = changed,
        };
        dropdown.AttachNode(row);
        _form.AddNode(row);
        return dropdown;
    }

    private CheckboxNode AddCheckbox(string label, string tooltip, Action<bool> changed)
    {
        var checkbox = new CheckboxNode { Height = 18, String = label, TextTooltip = tooltip, OnClick = changed };
        _form.AddNode(checkbox);
        return checkbox;
    }

    internal void SelectSource(int source)
    {
        _selectedSource = source;
        SyncControls();
    }

    private void SetUseGlobal(bool value)
    {
        if (_syncing || _selectedSource == 0) return;
        if (value)
        {
            Settings.UseGlobal = true;
        }
        else
        {
            var copy = System.Config.General.Layout.Copy();
            copy.UseGlobal = false;
            switch (_selectedSource)
            {
                case 1: System.Config.General.InventoryLayout = copy; break;
                case 2: System.Config.General.SaddleBagLayout = copy; break;
                case 3: System.Config.General.RetainerLayout = copy; break;
            }
        }
        SyncControls();
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void Edit(Action<InventoryLayoutSettings> edit)
    {
        if (_syncing || IsInherited) return;
        edit(Settings);
        SyncControls();
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void SyncControls()
    {
        _syncing = true;
        try
        {
            bool inherited = IsInherited;
            var settings = inherited ? System.Config.General.Layout : Settings;
            _useGlobal.IsVisible = _selectedSource != 0;
            _useGlobal.IsChecked = inherited;
            _updates.SelectedOption = settings.Updates;
            _updates.IsEnabled = !inherited;
            _retention.SelectedOption = settings.Retention;
            _retention.IsEnabled = !inherited;
            _idleDelay.Value = Math.Clamp(settings.SettleDelayMilliseconds, 250, 10000);
            _idleDelay.IsEnabled = !inherited && settings.Updates == InventoryLayoutUpdates.AfterIdle;
            for (int index = 0; index < _contexts.Length; index++)
            {
                _contexts[index].IsChecked = (settings.Contexts & ContextOptions[index].Flag) != 0;
                _contexts[index].IsEnabled = !inherited;
            }
            _showEmptySlots.IsChecked = settings.ShowEmptySlots;
            _showEmptySlots.IsEnabled = !inherited;
            _separateRows.IsChecked = settings.SeparateCategoryRows;
            _separateRows.IsEnabled = !inherited;
        }
        finally
        {
            _syncing = false;
        }
        using var visibility = new LayoutVisibilityScope(this);
        RecalculateLayout();
        _onLayoutChanged?.Invoke();
    }
}
