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

        AddNode();

        CheckboxNode cappedEnabledCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Color When Capped",
            IsChecked = config.ColorWhenCapped,
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
            Label = "Capped Currency Color",
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
            String = "Color Weekly Limit",
            IsChecked = config.ColorWhenLimited,
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
            Label = "Limit Currency Color",
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