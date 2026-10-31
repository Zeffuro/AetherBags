using System;
using System.Numerics;
using System.Threading.Tasks;
using AetherBags.Addons.Config;
using Dalamud.Plugin.Services;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using KeybindDefinition = AetherBags.Configuration.Keybind;

namespace AetherBags.Nodes.Configuration.Keybind;

public enum KeybindDialogKind { FocusSearch, OpenBags, OpenSaddlebags }

public static class KeybindOptionNode
{
    private const float RowWidth = 500.0f;
    private const float RowHeight = 28.0f;
    private const float CheckboxWidth = 300.0f;
    private const float ButtonWidth = 150.0f;

    private static readonly DialogSlot FocusSearch = new();
    private static readonly DialogSlot OpenBags = new();
    private static readonly DialogSlot OpenSaddlebags = new();

    public static NodeBase Create(
        KeybindDialogKind kind,
        string label,
        bool initialEnabled,
        KeybindDefinition initialKeybind,
        Action<bool> onToggle,
        Action<KeybindDefinition> onSaveKeybind,
        bool showGameConflicts = true)
    {
        var currentKeybind = initialKeybind;
        var slot = GetSlot(kind);
        var container = new KeybindRowNode
        {
            Slot = slot,
            Size = new Vector2(RowWidth, RowHeight),
            ItemSpacing = 8.0f,
        };
        var buttonNode = new TextButtonNode
        {
            String = currentKeybind.ToString(),
            Size = new Vector2(ButtonWidth, 24),
            IsEnabled = initialEnabled,
        };
        var checkboxNode = new CheckboxNode
        {
            String = label,
            IsChecked = initialEnabled,
            Size = new Vector2(CheckboxWidth, 18),
            OnClick = isChecked =>
            {
                onToggle(isChecked);
                buttonNode.IsEnabled = isChecked;
            },
        };

        buttonNode.OnClick = () =>
        {
            if (Plugin.IsUnloading || !container.IsAlive) return;
            if (CloseAllocatedDialogs()) return;

            var addon = slot.Dialog ??= new KeybindConfigAddon
            {
                InternalName = $"AetherBags_KeybindConfig_{kind}",
                Title = "Set Keybind",
                ShowGameConflicts = showGameConflicts,
            };
            slot.Owner = container;
            addon.InitialKeybind = currentKeybind;
            addon.OnKeybindChanged = newKeybind =>
            {
                if (Plugin.IsUnloading || !container.IsAlive || !ReferenceEquals(slot.Owner, container)) return;
                currentKeybind = newKeybind;
                buttonNode.String = newKeybind.ToString();
                onSaveKeybind(newKeybind);
            };
            addon.Open();
        };

        container.AddNode(checkboxNode);
        container.AddNode(buttonNode);
        return container;
    }

    private static DialogSlot GetSlot(KeybindDialogKind kind) => kind switch
    {
        KeybindDialogKind.FocusSearch => FocusSearch,
        KeybindDialogKind.OpenBags => OpenBags,
        KeybindDialogKind.OpenSaddlebags => OpenSaddlebags,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool CloseAllocatedDialogs()
    {
        var hadAllocatedDialog = false;
        foreach (var slot in new[] { FocusSearch, OpenBags, OpenSaddlebags })
        {
            if (slot.Dialog is not { IsAllocated: true } dialog) continue;
            dialog.Close();
            hadAllocatedDialog = true;
        }
        return hadAllocatedDialog;
    }

    public static async ValueTask DisposeDialogsAsync()
    {
        var slots = new[] { FocusSearch, OpenBags, OpenSaddlebags };
        void CloseDialogs()
        {
            foreach (var slot in slots)
            {
                slot.Owner = null;
                if (slot.Dialog is not { } dialog) continue;
                dialog.OnKeybindChanged = null;
                if (!IFramework.Get().IsFrameworkUnloading) dialog.Close();
            }
        }

        if (!IFramework.Get().IsFrameworkUnloading)
            await IFramework.Get().Run(CloseDialogs).ConfigureAwait(false);
        else
            CloseDialogs();

        foreach (var slot in slots)
        {
            if (slot.Dialog is not { } dialog) continue;
            await dialog.DisposeAsync().ConfigureAwait(false);
            slot.Dialog = null;
        }
    }

    private sealed class DialogSlot
    {
        public KeybindConfigAddon? Dialog { get; set; }
        public KeybindRowNode? Owner { get; set; }
    }

    private sealed class KeybindRowNode : HorizontalListNode
    {
        public required DialogSlot Slot { get; init; }
        public bool IsAlive => !IsDisposed;

        protected override void Dispose(bool isNativeDestructor)
        {
            if (ReferenceEquals(Slot.Owner, this))
            {
                Slot.Owner = null;
                if (Slot.Dialog is { } dialog)
                {
                    dialog.OnKeybindChanged = null;
                    dialog.Close();
                }
            }
            base.Dispose(isNativeDestructor);
        }
    }
}
