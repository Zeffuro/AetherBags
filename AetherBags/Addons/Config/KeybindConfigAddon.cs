using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using GameKeybind = FFXIVClientStructs.FFXIV.Client.System.Input.Keybind;
using PluginKeybind = AetherBags.Configuration.Keybind;

namespace AetherBags.Addons.Config;

// Heavily inspired/copied/adapted from VanillaPlus: https://github.com/MidoriKami/VanillaPlus/blob/e3d84bc08b54d48714543d629b561b574952e192/VanillaPlus/NativeElements/Addons/KeybindConfigAddon.cs#L17

public class KeybindConfigAddon : NativeAddon
{
    private const float DefaultWindowWidth = 360.0f;
    private const float DefaultWindowHeight = 160.0f;
    private const float ConflictWindowWidth = 420.0f;
    private const float ConflictWindowHeight = 260.0f;
    private const float ButtonWidth = 100.0f;
    private const float ButtonHeight = 24.0f;

    private CategoryTextNode? _inputComboLabelNode;
    private HorizontalLineNode? _topLineNode;
    private TextNode? _currentComboTextNode;
    private CategoryTextNode? _conflictsLabelNode;
    private HorizontalLineNode? _conflictsLineNode;
    private ScrollingListNode? _conflictsScrollingAreaNode;
    private HorizontalLineNode? _buttonsLineNode;
    private TextButtonNode? _confirmButtonNode;
    private TextButtonNode? _cancelButtonNode;

    private HashSet<VirtualKey> _combo = [];
    private VirtualKey[] _validKeys = [];

    public required PluginKeybind InitialKeybind { get; init; }

    public required Action<PluginKeybind> OnKeybindChanged { get; init; }

    public bool ShowGameConflicts { get; init; } = true;

    public static bool IsCapturingKeybind { get; private set; }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        SetWindowSize(
            ShowGameConflicts ? ConflictWindowWidth : DefaultWindowWidth,
            ShowGameConflicts ? ConflictWindowHeight : DefaultWindowHeight);
        IsCapturingKeybind = true;

        _combo = InitialKeybind.Modifiers.ToHashSet();
        if (InitialKeybind.Key != VirtualKey.NO_KEY)
        {
            _combo.Add(InitialKeybind.Key);
        }

        _validKeys = Services.KeyState.GetValidVirtualKeys().Where(key => key.IsBindable()).ToArray();

        _inputComboLabelNode = new CategoryTextNode
        {
            Height = 18,
            AlignmentType = AlignmentType.Left,
            Position = ContentStartPosition,
            String = "Press desired key combination:",
        };
        _inputComboLabelNode.AttachNode(this);

        _topLineNode = new HorizontalLineNode
        {
            Position = ContentStartPosition + new Vector2(2.0f, 24.0f),
            Size = new Vector2(ContentSize.X - 4.0f, 2.0f),
        };
        _topLineNode.AttachNode(this);

        _currentComboTextNode = new TextNode
        {
            Position = ContentStartPosition + new Vector2(0.0f, 32.0f),
            Size = new Vector2(ContentSize.X, 42.0f),
            FontSize = 16,
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.Center,
            TextColor = ColorHelper.GetColor(3),
            TextOutlineColor = ColorHelper.GetColor(0),
            String = GetComboString(),
        };
        _currentComboTextNode.AttachNode(this);

        if (ShowGameConflicts)
        {
            _conflictsLabelNode = new CategoryTextNode
            {
                Height = 18,
                AlignmentType = AlignmentType.Left,
                Position = ContentStartPosition + new Vector2(0.0f, 84.0f),
                String = "Game Keybind Conflicts:",
            };
            _conflictsLabelNode.AttachNode(this);

            _conflictsLineNode = new HorizontalLineNode
            {
                Position = ContentStartPosition + new Vector2(2.0f, 108.0f),
                Size = new Vector2(ContentSize.X - 4.0f, 2.0f),
            };
            _conflictsLineNode.AttachNode(this);

            _conflictsScrollingAreaNode = new ScrollingListNode
            {
                Position = ContentStartPosition + new Vector2(0.0f, 116.0f),
                Size = new Vector2(ContentSize.X, 54.0f),
                AutoHideScrollBar = true,
            };
            _conflictsScrollingAreaNode.AttachNode(this);
            UpdateConflicts();
        }

        _buttonsLineNode = new HorizontalLineNode
        {
            Position = ContentStartPosition + new Vector2(2.0f, ContentSize.Y - 40.0f),
            Size = new Vector2(ContentSize.X - 4.0f, 2.0f),
        };
        _buttonsLineNode.AttachNode(this);

        _confirmButtonNode = new TextButtonNode
        {
            Position = ContentStartPosition + new Vector2(0.0f, ContentSize.Y - ButtonHeight),
            Size = new Vector2(ButtonWidth, ButtonHeight),
            String = "Confirm",
            OnClick = ConfirmKeybind,
        };
        _confirmButtonNode.AttachNode(this);

        _cancelButtonNode = new TextButtonNode
        {
            Position = ContentStartPosition + new Vector2(ContentSize.X - ButtonWidth, ContentSize.Y - ButtonHeight),
            Size = new Vector2(ButtonWidth, ButtonHeight),
            String = "Cancel",
            OnClick = Close,
        };
        _cancelButtonNode.AttachNode(this);
    }

    protected override unsafe void OnUpdate(AtkUnitBase* addon)
    {
        if (!IsOpen) return;

        var pressedKeys = _validKeys.Where(key => Services.KeyState[key]).ToHashSet();
        if (pressedKeys.Any(key => key.IsKey()) && !_combo.SetEquals(pressedKeys))
        {
            _combo = pressedKeys;
            UpdateText();
            if (ShowGameConflicts)
                UpdateConflicts();
        }

        base.OnUpdate(addon);
    }

    private void ConfirmKeybind()
    {
        var newKeybind = new PluginKeybind
        {
            Key = _combo.FirstOrDefault(key => key.IsKey(), VirtualKey.NO_KEY),
            Modifiers = _combo.Where(key => key.IsModifier()).ToHashSet(),
        };

        OnKeybindChanged(newKeybind);
        Close();
    }

    private void UpdateText()
    {
        if (_currentComboTextNode != null)
            _currentComboTextNode.String = GetComboString();
    }

    private string GetComboString()
    {
        return _combo.Count == 0
            ? "None"
            : string.Join(" + ", _combo.OrderByModifier().Select(key => key.GetDisplayName()));
    }

    private void UpdateConflicts()
    {
        if (!ShowGameConflicts || _conflictsScrollingAreaNode == null) return;

        _conflictsScrollingAreaNode.Clear();

        var conflicts = GetGameConflicts().ToList();
        if (conflicts.Count == 0)
        {
            _conflictsScrollingAreaNode.AddNode(new TextNode
            {
                Height = 18,
                String = "No conflicts found.",
                TextColor = ColorHelper.GetColor(3),
            });
        }
        else
        {
            foreach (var conflict in conflicts)
            {
                _conflictsScrollingAreaNode.AddNode(new TextNode
                {
                    Height = 18,
                    String = conflict.ToString(),
                    TextColor = ColorHelper.GetColor(17),
                });
            }
        }

        _conflictsScrollingAreaNode.RecalculateLayout();
    }

    private unsafe List<InputId> GetGameConflicts()
    {
        var conflicts = new List<InputId>();
        if (!_combo.Any(key => key.IsKey())) return conflicts;

        var keybindSpan = UIInputData.Instance()->GetKeybindSpan();
        for (var index = 0; index < keybindSpan.Length; index++)
        {
            ref var keybind = ref keybindSpan[index];
            if (IsGameKeybindMatch(keybind, _combo))
                conflicts.Add((InputId)index);
        }

        return conflicts;
    }

    private static bool IsGameKeybindMatch(GameKeybind keybind, HashSet<VirtualKey> keyCombo)
    {
        foreach (var keySetting in keybind.KeySettings)
        {
            if (IsGameKeySettingMatch(keySetting, keyCombo)) return true;
        }

        return false;
    }

    private static bool IsGameKeySettingMatch(KeySetting keySetting, HashSet<VirtualKey> keyCombo)
    {
        var comboKey = keyCombo.FirstOrDefault(key => key.IsKey(), VirtualKey.NO_KEY);
        var comboModifiers = keyCombo.Where(key => key.IsModifier()).ToHashSet();

        if ((int)comboKey != (int)keySetting.Key) return false;

        var modifier = keySetting.KeyModifier;
        if (modifier is KeyModifierFlag.None && comboModifiers.Count != 0) return false;
        if (modifier.HasFlag(KeyModifierFlag.Ctrl) && !comboModifiers.Contains(VirtualKey.CONTROL)) return false;
        if (modifier.HasFlag(KeyModifierFlag.Alt) && !comboModifiers.Contains(VirtualKey.MENU)) return false;
        if (modifier.HasFlag(KeyModifierFlag.Shift) && !comboModifiers.Contains(VirtualKey.SHIFT)) return false;

        return true;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        IsCapturingKeybind = false;

        base.OnFinalize(addon);
    }
}
