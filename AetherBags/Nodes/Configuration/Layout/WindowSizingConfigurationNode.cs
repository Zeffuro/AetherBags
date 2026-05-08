using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using KamiToolKit.Premade.Node.Simple;

namespace AetherBags.Nodes.Configuration.Layout;

internal sealed class WindowSizingConfigurationNode : TabbedVerticalListNode
{
    private static readonly WindowSizingDefinition[] Definitions =
    [
        new("Inventory", config => config.InventoryWindowSizing, InventoryWindowSizingLimits.Inventory),
        new("Saddlebags", config => config.SaddleBagWindowSizing, InventoryWindowSizingLimits.SaddleBag),
        new("Retainers", config => config.RetainerWindowSizing, InventoryWindowSizingLimits.Retainer),
    ];

    public WindowSizingConfigurationNode()
    {
        GeneralSettings config = System.Config.General;

        var titleNode = new CategoryTextNode
        {
            Height = 18,
            String = "Window Sizing",
        };
        AddNode(titleNode);

        AddTab(1);
        foreach (var definition in Definitions)
        {
            AddNode(new WindowSizingRowNode(definition, config)
            {
                Width = WindowSizingRowNode.RowWidth,
                OnLayoutChanged = RecalculateLayout,
            });
        }

        SubtractTab(1);
    }

    internal static void NotifySizingChanged() => InventoryOrchestrator.RefreshAll(updateMaps: false);
}

internal readonly record struct WindowSizingDefinition(
    string Label,
    Func<GeneralSettings, InventoryWindowSizingSettings> GetSettings,
    InventoryWindowSizingLimits Limits);

internal sealed class WindowSizingRowNode : SimpleComponentNode
{
    internal const float RowWidth = 560.0f;

    private const float CompactRowHeight = 30.0f;
    private const float BoundsRowHeight = 58.0f;
    private const float LabelWidth = 86.0f;
    private const float LabelYOffset = 4.0f;
    private const float DropDownX = 92.0f;
    private const float DropDownYOffset = 4.0f;
    private const float DropDownWidth = 136.0f;
    private const float DropDownHeight = 22.0f;
    private const float InputStartX = 238.0f;
    private const float InputSecondColumnX = 356.0f;
    private const float InputSecondRowY = 30.0f;
    private const float InputWidth = 108.0f;
    private const float InputHeight = 28.0f;

    private readonly EnumDropDownNode<InventoryWindowSizingMode> _modeDropDownNode;
    private readonly CompactSizingInputNode _fixedWidthNode;
    private readonly CompactSizingInputNode _fixedHeightNode;
    private readonly CompactSizingInputNode _minWidthNode;
    private readonly CompactSizingInputNode _maxWidthNode;
    private readonly CompactSizingInputNode _minHeightNode;
    private readonly CompactSizingInputNode _maxHeightNode;
    private readonly InventoryWindowSizingSettings _settings;
    private readonly InventoryWindowSizingLimits _limits;
    private bool _syncing;

    internal Action? OnLayoutChanged { private get; init; }

    public WindowSizingRowNode(WindowSizingDefinition definition, GeneralSettings config)
    {
        Size = new Vector2(RowWidth, CompactRowHeight);

        _settings = definition.GetSettings(config);
        _limits = definition.Limits;
        InventoryWindowSizingDefaults.Normalize(_settings, _limits);

        var labelNode = new LabelTextNode
        {
            Position = new Vector2(0, LabelYOffset),
            Size = new Vector2(LabelWidth, DropDownHeight),
            String = definition.Label,
        };
        labelNode.AttachNode(this);

        _modeDropDownNode = new EnumDropDownNode<InventoryWindowSizingMode>
        {
            Position = new Vector2(DropDownX, DropDownYOffset),
            Size = new Vector2(DropDownWidth, DropDownHeight),
            IsVisible = true,
            Options = Enum.GetValues<InventoryWindowSizingMode>().ToList(),
            OnOptionSelected = OnModeSelected,
        };
        _modeDropDownNode.AttachNode(this);

        _fixedWidthNode = new CompactSizingInputNode
        {
            Position = new Vector2(InputStartX, 0),
            Size = new Vector2(InputWidth, InputHeight),
            Label = "W",
            Min = _limits.SafeMinWidth,
            Max = InventoryWindowSizingDefaults.MaxConfigurableWidth,
            OnValueUpdate = OnFixedWidthUpdated,
        };
        _fixedWidthNode.AttachNode(this);

        _fixedHeightNode = new CompactSizingInputNode
        {
            Position = new Vector2(InputSecondColumnX, 0),
            Size = new Vector2(InputWidth, InputHeight),
            Label = "H",
            Min = _limits.SafeMinHeight,
            Max = InventoryWindowSizingDefaults.MaxConfigurableHeight,
            OnValueUpdate = OnFixedHeightUpdated,
        };
        _fixedHeightNode.AttachNode(this);

        _minWidthNode = new CompactSizingInputNode
        {
            Position = new Vector2(InputStartX, 0),
            Size = new Vector2(InputWidth, InputHeight),
            Label = "W",
            Min = _limits.SafeMinWidth,
            Max = InventoryWindowSizingDefaults.MaxConfigurableWidth,
            OnValueUpdate = OnMinWidthUpdated,
        };
        _minWidthNode.AttachNode(this);

        _maxWidthNode = new CompactSizingInputNode
        {
            Position = new Vector2(InputSecondColumnX, 0),
            Size = new Vector2(InputWidth, InputHeight),
            Label = "–",
            Min = _limits.SafeMinWidth,
            Max = InventoryWindowSizingDefaults.MaxConfigurableWidth,
            OnValueUpdate = OnMaxWidthUpdated,
        };
        _maxWidthNode.AttachNode(this);

        _minHeightNode = new CompactSizingInputNode
        {
            Position = new Vector2(InputStartX, InputSecondRowY),
            Size = new Vector2(InputWidth, InputHeight),
            Label = "H",
            Min = _limits.SafeMinHeight,
            Max = InventoryWindowSizingDefaults.MaxConfigurableHeight,
            OnValueUpdate = OnMinHeightUpdated,
        };
        _minHeightNode.AttachNode(this);

        _maxHeightNode = new CompactSizingInputNode
        {
            Position = new Vector2(InputSecondColumnX, InputSecondRowY),
            Size = new Vector2(InputWidth, InputHeight),
            Label = "–",
            Min = _limits.SafeMinHeight,
            Max = InventoryWindowSizingDefaults.MaxConfigurableHeight,
            OnValueUpdate = OnMaxHeightUpdated,
        };
        _maxHeightNode.AttachNode(this);

        SetDropDownSelection(_settings.Mode);
        SyncInputs();
        UpdateVisibleInputs();
    }

    private void OnFixedWidthUpdated(int value)
    {
        _settings.FixedWidth = value;
        ApplySizingUpdate();
    }

    private void OnFixedHeightUpdated(int value)
    {
        _settings.FixedHeight = value;
        ApplySizingUpdate();
    }

    private void OnMinWidthUpdated(int value)
    {
        _settings.MinWidth = value;
        ApplySizingUpdate();
    }

    private void OnMaxWidthUpdated(int value)
    {
        _settings.MaxWidth = value;
        ApplySizingUpdate();
    }

    private void OnMinHeightUpdated(int value)
    {
        _settings.MinHeight = value;
        ApplySizingUpdate();
    }

    private void OnMaxHeightUpdated(int value)
    {
        _settings.MaxHeight = value;
        ApplySizingUpdate();
    }

    private void ApplySizingUpdate()
    {
        if (_syncing) return;

        InventoryWindowSizingDefaults.Normalize(_settings, _limits);
        SyncInputs();
        WindowSizingConfigurationNode.NotifySizingChanged();
    }

    private void OnModeSelected(InventoryWindowSizingMode selected)
    {
        _settings.Mode = selected;
        SetDropDownSelection(selected);
        UpdateVisibleInputs();
        WindowSizingConfigurationNode.NotifySizingChanged();
    }

    private void SetDropDownSelection(InventoryWindowSizingMode mode)
    {
        _modeDropDownNode.OptionListNode.SelectedOption = mode;
        _modeDropDownNode.LabelNode.String = mode.Description;
    }

    private void SyncInputs()
    {
        _syncing = true;
        _fixedWidthNode.Value = _settings.FixedWidth;
        _fixedHeightNode.Value = _settings.FixedHeight;
        _minWidthNode.Value = _settings.MinWidth;
        _maxWidthNode.Value = _settings.MaxWidth;
        _minHeightNode.Value = _settings.MinHeight;
        _maxHeightNode.Value = _settings.MaxHeight;
        _syncing = false;
    }

    private void UpdateVisibleInputs()
    {
        bool fixedVisible = _settings.Mode == InventoryWindowSizingMode.Fixed;
        bool boundsVisible = _settings.Mode == InventoryWindowSizingMode.CustomBounds;

        _fixedWidthNode.IsVisible = fixedVisible;
        _fixedHeightNode.IsVisible = fixedVisible;
        _minWidthNode.IsVisible = boundsVisible;
        _maxWidthNode.IsVisible = boundsVisible;
        _minHeightNode.IsVisible = boundsVisible;
        _maxHeightNode.IsVisible = boundsVisible;

        var rowHeight = boundsVisible ? BoundsRowHeight : CompactRowHeight;
        if (Math.Abs(Height - rowHeight) < 0.1f) return;

        Height = rowHeight;
        OnLayoutChanged?.Invoke();
    }
}

internal sealed class CompactSizingInputNode : SimpleComponentNode
{
    private const float LabelWidth = 18.0f;
    private const float LabelInputGap = 2.0f;

    private readonly LabelTextNode _labelNode;
    private readonly NumericInputNode _inputNode;

    public CompactSizingInputNode()
    {
        _labelNode = new LabelTextNode
        {
            Position = new Vector2(0, 4),
            Size = new Vector2(LabelWidth, 20),
        };
        _labelNode.AttachNode(this);

        _inputNode = new NumericInputNode
        {
            Position = new Vector2(LabelWidth + LabelInputGap, 0),
            Size = new Vector2(88, 28),
            IsVisible = true,
        };
        _inputNode.AttachNode(this);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        _labelNode.Position = new Vector2(0, Math.Max(0.0f, (Height - 20.0f) / 2.0f));
        _labelNode.Size = new Vector2(LabelWidth, 20.0f);
        _inputNode.Position = new Vector2(LabelWidth + LabelInputGap, 0);
        _inputNode.Size = new Vector2(Math.Max(0.0f, Width - LabelWidth - LabelInputGap), Height);
    }

    public string Label
    {
        get => _labelNode.String.ExtractText();
        set => _labelNode.String = value;
    }

    public int Value
    {
        get => _inputNode.Value;
        set => _inputNode.Value = value;
    }

    public int Min
    {
        get => _inputNode.Min;
        set => _inputNode.Min = value;
    }

    public int Max
    {
        get => _inputNode.Max;
        set => _inputNode.Max = value;
    }

    public Action<int>? OnValueUpdate
    {
        get => _inputNode.OnValueUpdate;
        set => _inputNode.OnValueUpdate = value;
    }
}
