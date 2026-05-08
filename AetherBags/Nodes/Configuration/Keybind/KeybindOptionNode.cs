using System;
using System.Numerics;
using AetherBags.Addons.Config;
using KamiToolKit;
using KamiToolKit.Nodes;
using KeybindDefinition = AetherBags.Configuration.Keybind;

namespace AetherBags.Nodes.Configuration.Keybind;

public static class KeybindOptionNode
{
    private const float RowWidth = 500.0f;
    private const float RowHeight = 28.0f;
    private const float CheckboxWidth = 300.0f;
    private const float ButtonWidth = 150.0f;

    public static NodeBase Create(
        string label,
        bool initialEnabled,
        KeybindDefinition initialKeybind,
        Action<bool> onToggle,
        Action<KeybindDefinition> onSaveKeybind,
        bool showGameConflicts = true)
    {
        var currentKeybind = initialKeybind;

        var container = new HorizontalListNode
        {
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
            var addon = new KeybindConfigAddon
            {
                InternalName = $"AetherBags_KeybindConfig_{Guid.NewGuid():N}",
                Title = "Set Keybind",
                InitialKeybind = currentKeybind,
                ShowGameConflicts = showGameConflicts,
                OnKeybindChanged = newKeybind =>
                {
                    currentKeybind = newKeybind;
                    buttonNode.String = newKeybind.ToString();
                    onSaveKeybind(newKeybind);
                },
            };

            addon.Open();
        };

        container.AddNode(checkboxNode);
        container.AddNode(buttonNode);

        return container;
    }
}
