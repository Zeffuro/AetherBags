using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.BaseTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiToolKit.Addons;

namespace AetherBags.Nodes.Color;

public class ColorInputRow : HorizontalListNode
{
    private static SharedColorPickerAddon? _colorPickerAddon;
    private static ColorInputRow? _activeOwner;
    private readonly LabelTextNode _labelTextNode;
    private readonly ColorPreviewButtonNode _colorPreview;

    public ColorInputRow()
    {
        _colorPreview = new ColorPreviewButtonNode { Size = new Vector2(28) };
        _labelTextNode = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Position = new Vector2(28, 0),
            Height = 28,
        };

        _colorPreview.OnClick = OpenColorPicker;

        _colorPreview.AttachNode(this);
        _labelTextNode.AttachNode(this);
    }

    private void OpenColorPicker()
    {
        if (Plugin.IsUnloading || !IsEnabled || IsDisposed) return;

        var picker = _colorPickerAddon ??= new SharedColorPickerAddon
        {
            InternalName = "ColorPicker_AetherBags",
            Title = "Pick a color",
        };
        if (picker.IsAllocated)
        {
            picker.Close();
            return;
        }

        var snapshot = CurrentColor;
        _activeOwner = this;
        picker.InitialColor = snapshot;
        picker.DefaultColor = DefaultColor;
        picker.OnColorConfirmed = color =>
        {
            picker.IsSessionCompleted = true;
            if (!OwnsSession) return;
            CurrentColor = color;
            OnColorConfirmed?.Invoke(color);
        };
        picker.OnColorPreviewed = color =>
        {
            if (!OwnsSession) return;
            _colorPreview.Color = color;
            OnColorPreviewed?.Invoke(color);
        };
        picker.OnColorCancelled = () =>
        {
            picker.IsSessionCompleted = true;
            if (!OwnsSession) return;
            CurrentColor = snapshot;
            OnColorCanceled?.Invoke(snapshot);
        };
        picker.Open();
    }

    private bool OwnsSession => !IsDisposed && ReferenceEquals(_activeOwner, this);

    private static void ClearSession()
    {
        _activeOwner = null;
        if (_colorPickerAddon is not { } picker) return;
        picker.OnColorConfirmed = null;
        picker.OnColorPreviewed = null;
        picker.OnColorCancelled = null;
    }

    public static void CancelActiveSession()
    {
        if (_activeOwner is null || _colorPickerAddon is not { } picker) return;
        if (!picker.IsSessionCompleted) picker.OnColorCancelled?.Invoke();
        ClearSession();
        picker.Close();
    }

    public static unsafe void CancelActiveSession(NodeBase scope)
    {
        if (scope is null) return;
        AtkResNode* scopeNode = scope;
        var manager = RaptureAtkUnitManager.Instance();
        if (scopeNode is null || manager is null) return;
        CancelActiveSession(manager->GetAddonByNode(scopeNode));
    }

    public static unsafe void CancelActiveSession(AtkUnitBase* addon)
    {
        if (addon is null || _activeOwner is not { } owner || owner.IsDisposed) return;
        AtkResNode* ownerNode = owner;
        var manager = RaptureAtkUnitManager.Instance();
        if (ownerNode is null || manager is null) return;
        if (manager->GetAddonByNode(ownerNode) == addon) CancelActiveSession();
    }

    public static async ValueTask DisposeSharedColorPickerAsync()
    {
        var picker = _colorPickerAddon;
        if (picker is null) return;

        if (!IFramework.Get().IsFrameworkUnloading)
            await IFramework.Get().Run(() =>
            {
                CancelActiveSession();
                ClearSession();
                picker.Close();
            }).ConfigureAwait(false);
        else
            ClearSession();

        await picker.DisposeAsync().ConfigureAwait(false);
        _colorPickerAddon = null;
    }

    protected override void Dispose(bool isNativeDestructor)
    {
        if (ReferenceEquals(_activeOwner, this))
        {
            ClearSession();
            _colorPickerAddon?.Close();
        }
        base.Dispose(isNativeDestructor);
    }

    private sealed class SharedColorPickerAddon : ColorPickerAddon
    {
        public unsafe bool IsAllocated => InternalAddon is not null;
        public bool IsSessionCompleted { get; set; }

        protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> values)
        {
            IsSessionCompleted = false;
            base.OnSetup(addon, values);
        }

        protected override unsafe void OnHide(AtkUnitBase* addon)
        {
            // Upstream retains its cancellation flag between allocations.
            if (IsSessionCompleted) return;
            OnColorPreviewed?.Invoke(InitialColor);
            OnColorCancelled?.Invoke();
        }

        protected override unsafe void OnFinalize(AtkUnitBase* addon)
        {
            ClearSession();
            base.OnFinalize(addon);
        }
    }

    public bool IsEnabled
    {
        get => _colorPreview.IsEnabled;
        set
        {
            _colorPreview.IsEnabled = value;
            _labelTextNode.Alpha = value ? 1.0f : 0.5f;
        }
    }

    public required string Label
    {
        get;
        set
        {
            field = value;
            _labelTextNode.String = value;
        }
    }

    public required Vector4 CurrentColor
    {
        get;
        set
        {
            field = value;
            _colorPreview.Color = value;
        }
    }

    public required Vector4 DefaultColor { get; set; }
    public Action<Vector4>? OnColorConfirmed { get; set; }
    public Action<Vector4>? OnColorCanceled { get; set; }
    public Action<Vector4>? OnColorChange { get; set; }
    public Action<Vector4>? OnColorPreviewed { get; set; }
}
