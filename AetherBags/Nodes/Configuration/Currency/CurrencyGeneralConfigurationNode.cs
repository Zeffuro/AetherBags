using System;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Configuration;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Configuration.Category;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes.Configuration.Currency;

public sealed class CurrencyGeneralConfigurationNode : TabbedVerticalListNode
{
    private readonly UintListEditorNode? _currencyListEditor;

    public CurrencyGeneralConfigurationNode()
    {
        CurrencySettings config = System.Config.Currency;

        Width = 600;
        ItemVerticalSpacing = 2;

        LabelTextNode titleNode = new LabelTextNode
        {
            Size = new Vector2(Width, 18),
            String = "Currency Configuration",
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(0),
        };
        AddNode(titleNode);

        AddTab(1);

        CheckboxNode currencyEnabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 18),
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

        var defaultColorHandler = CreateColorHandler(color => config.DefaultColor = color);
        ColorInputRow defaultCurrencyColorNode = new ColorInputRow
        {
            Label = "Default Currency Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.DefaultColor,
            DefaultColor = new CurrencySettings().DefaultColor,
            OnColorConfirmed = defaultColorHandler,
            OnColorChange = defaultColorHandler,
            OnColorCanceled = defaultColorHandler,
            OnColorPreviewed = defaultColorHandler,
        };
        AddNode(defaultCurrencyColorNode);

        CheckboxNode cappedEnabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 18),
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

        var cappedColorHandler = CreateColorHandler(color => config.CappedColor = color);
        ColorInputRow cappedCurrencyColorNode = new ColorInputRow
        {
            Label = "Weekly Cap Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.CappedColor,
            DefaultColor = new CurrencySettings().CappedColor,
            OnColorConfirmed = cappedColorHandler,
            OnColorChange = cappedColorHandler,
            OnColorCanceled = cappedColorHandler,
            OnColorPreviewed = cappedColorHandler,
        };
        AddNode(cappedCurrencyColorNode);

        SubtractTab(1);

        CheckboxNode limitedEnabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 18),
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

        var limitColorHandler = CreateColorHandler(color => config.LimitColor = color);
        ColorInputRow limitCurrencyColorNode = new ColorInputRow
        {
            Label = "Max Capacity Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.LimitColor,
            DefaultColor = new CurrencySettings().LimitColor,
            OnColorConfirmed = limitColorHandler,
            OnColorChange = limitColorHandler,
            OnColorCanceled = limitColorHandler,
            OnColorPreviewed = limitColorHandler,
        };
        AddNode(limitCurrencyColorNode);

        AddNode(new ResNode { Size = new Vector2(15) });

        SubtractTab(2);

        AddNode(new ResNode { Size = new Vector2(15) });

        _currencyListEditor = new UintListEditorNode
        {
            Label = "Displayed Currencies:",
            LabelResolver = id =>
            {
                return id switch
                {
                    CurrencySettings.LimitedTomestoneId => "Current Limited Tomestone",
                    CurrencySettings.NonLimitedTomestoneId => "Current Non-Limited Tomestone",
                    _ => Services.DataManager.GetExcelSheet<Item>().GetRow(id).Name.ToString()
                };
            },
            OnSearchButtonClicked = OpenCurrencyPicker,
            OnChanged = () => {
                System.Config.Currency.DisplayedCurrencies = _currencyListEditor!.GetList();
                RefreshCurrency();
                RecalculateLayout();
            }
        };
        _currencyListEditor.SetList(System.Config.Currency.DisplayedCurrencies);
        AddNode(_currencyListEditor);

        var quickAddRow = new HorizontalListNode { Size = new Vector2(600, 30), ItemSpacing = 8.0f };

        quickAddRow.AddNode(new TextButtonNode {
            String = "+ Gil", Size = new Vector2(70, 24),
            OnClick = () => _currencyListEditor?.AddValue(1)
        });

        quickAddRow.AddNode(new TextButtonNode {
            String = "+ Limited Tomestone", Size = new Vector2(150, 24),
            OnClick = () => _currencyListEditor?.AddValue(CurrencySettings.LimitedTomestoneId)
        });

        quickAddRow.AddNode(new TextButtonNode {
            String = "+ Non-Limited", Size = new Vector2(110, 24),
            OnClick = () => _currencyListEditor?.AddValue(CurrencySettings.NonLimitedTomestoneId)
        });
        AddNode(quickAddRow);
        RecalculateLayout();
    }

    private Action<Vector4> CreateColorHandler(Action<Vector4> setter) => newColor =>
    {
        setter(newColor);
        RefreshCurrency();
    };

    private void RefreshCurrency() => System.AddonInventoryWindow.ManualCurrencyRefresh();

    private void OpenCurrencyPicker() {
        var picker = new AddonCurrencyPicker
        {
            Title = "Select Currency to Add",
            InternalName = "AetherBags_CurrencyPicker",
        };
        picker.SelectionResult = item => _currencyListEditor?.AddValue(item.RowId);
        picker.Open();
    }
}