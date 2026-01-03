using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Nodes.Color;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Currency;

public sealed class CurrencyGeneralConfigurationNode : TabbedVerticalListNode
{
    public CurrencyGeneralConfigurationNode()
    {
        CurrencySettings config = System.Config.Currency;

        ItemVerticalSpacing = 2;

        LabelTextNode titleNode = new LabelTextNode
        {
            Size = Size with { Y = 18 },
            String = "Currency Configuration",
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(0),
        };
        AddNode(titleNode);

        AddTab(1);

        CheckboxNode currencyEnabledCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Show Currency",
            IsChecked = config.Enabled,
            OnClick = isChecked =>
            {
                config.Enabled = isChecked;
                RefreshCurrency();
            }
        };
        AddNode(currencyEnabledCheckbox);

        AddTab(1);

        ColorInputRow defaultCurrencyColorNode = new ColorInputRow
        {
            Label = "Default Currency Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.DefaultColor,
            DefaultColor = new CurrencySettings().DefaultColor,
            OnColorConfirmed = ApplyColorChange,
            OnColorChange = ApplyColorChange,
            OnColorCanceled = ApplyColorChange,
        };
        AddNode(defaultCurrencyColorNode);

        CheckboxNode cappedEnabledCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Color Weekly Cap",
            IsChecked = config.ColorWhenCapped,
            TextTooltip = "Changes the color of the currency display when you have reached the maximum amount earnable for the current week (e.g., 450/450).",
            OnClick = isChecked =>
            {
                config.ColorWhenCapped = isChecked;
                RefreshCurrency();
            }
        };
        AddNode(cappedEnabledCheckbox);

        AddTab(1);


        ColorInputRow cappedCurrencyColorNode = new ColorInputRow
        {
            Label = "Weekly Cap Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.CappedColor,
            DefaultColor = new CurrencySettings().CappedColor,
            OnColorConfirmed = color =>
            {
                config.CappedColor = color;
                RefreshCurrency();
            },
        };
        AddNode(cappedCurrencyColorNode);

        SubtractTab(1);

        CheckboxNode limitedEnabledCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Color Max Capacity",
            IsChecked = config.ColorWhenLimited,
            TextTooltip = "Changes the color of the currency display when your total held amount has reached its maximum capacity (e.g., 2000/2000).",
            OnClick = isChecked =>
            {
                config.ColorWhenLimited = isChecked;
                RefreshCurrency();
            }
        };
        AddNode(limitedEnabledCheckbox);

        AddTab(1);

        ColorInputRow limitCurrencyColorNode = new ColorInputRow
        {
            Label = "Max Capacity Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.LimitColor,
            DefaultColor = new CurrencySettings().LimitColor,
            OnColorConfirmed = color =>
            {
                config.LimitColor = color;
                RefreshCurrency();
            },
        };
        AddNode(limitCurrencyColorNode);

        return;

        void ApplyColorChange(Vector4 color)
        {
            config.DefaultColor = color;
            RefreshCurrency();
        }
    }

    private void RefreshCurrency() => System.AddonInventoryWindow.ManualCurrencyRefresh();
}