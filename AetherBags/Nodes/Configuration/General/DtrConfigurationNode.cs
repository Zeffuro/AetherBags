using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Tags;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.General;

internal sealed class DtrConfigurationNode : TabbedVerticalListNode
{
    public DtrConfigurationNode()
    {
        DtrSettings config = System.Config.Dtr;

        ItemVerticalSpacing = 2;

        AddNode(new CategoryTextNode
        {
            Height = 18,
            String = "Server Info Bar",
        });

        var enabledCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Show slot usage in server info bar",
            TextTooltip = "Left click toggles your bags. Right click toggles your saddlebags.",
            IsChecked = config.Enabled,
            OnClick = isChecked =>
            {
                config.Enabled = isChecked;
                System.DtrService.UpdateBar();
            }
        };
        AddNode(1, enabledCheckbox);

        AddNode(new ResNode { Height = 6 });

        AddNode(2, new TextNode
        {
            Size = new Vector2(500, 18),
            String = "Format String:",
        });

        var formatInput = new TextInputNode
        {
            Size = new Vector2(500, 28),
            PlaceholderString = DtrSettings.DefaultFormatString,
            String = string.IsNullOrWhiteSpace(config.FormatString) ? DtrSettings.DefaultFormatString : config.FormatString,
            OnInputReceived = input =>
            {
                config.FormatString = input.ExtractText();
                System.DtrService.UpdateBar();
            },
            OnInputComplete = input =>
            {
                config.FormatString = input.ExtractText();
                System.DtrService.UpdateBar();
            },
        };
        AddNode(2, formatInput);

        AddNode(2, new TextNode
        {
            Size = new Vector2(500, 34),
            String = "Examples: [free] slots left, [used]/[total], [percent:0]%",
        });

        AddNode(2, new TextNode
        {
            Size = new Vector2(500, 18),
            String = "Insert Tag:",
        });

        var tagDropdown = new TextDropDownNode
        {
            Size = new Vector2(320, 24),
            Options = DtrTagDefinitions.GetLabels(),
            MaxListOptions = 8,
            SelectedOption = DtrTagDefinitions.DefaultDropdownLabel,
        };
        tagDropdown.OnOptionSelected = selected =>
        {
            if (!DtrTagDefinitions.Templates.TryGetValue(selected, out var tag) || string.IsNullOrEmpty(tag)) return;

            var current = formatInput.String.ExtractText();
            if (!string.IsNullOrEmpty(current) && !current.EndsWith(' '))
                current += " ";

            formatInput.String = current + tag;
            config.FormatString = formatInput.String.ExtractText();
            System.DtrService.UpdateBar();
            tagDropdown.SelectedOption = DtrTagDefinitions.DefaultDropdownLabel;
        };
        AddNode(2, tagDropdown);

        AddNode(2, new TextButtonNode
        {
            Size = new Vector2(120, 28),
            String = "Reset Format",
            OnClick = () =>
            {
                config.FormatString = DtrSettings.DefaultFormatString;
                formatInput.String = config.FormatString;
                System.DtrService.UpdateBar();
            },
        });
    }
}

