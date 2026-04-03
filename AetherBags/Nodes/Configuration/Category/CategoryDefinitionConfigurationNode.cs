using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Helpers.Import;
using AetherBags.Inventory;
using AetherBags.Nodes.Layout;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using KamiToolKit.Premade.Node.Simple;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryDefinitionConfigurationNode : SimpleComponentNode
{
    private static ExcelSheet<Item>? ItemSheet => Services.DataManager.GetExcelSheet<Item>();
    private static ExcelSheet<ItemUICategory>? UICategorySheet => Services.DataManager.GetExcelSheet<ItemUICategory>();

    public Action? OnLayoutChanged { get; init; }
    public Action? OnCategoryPropertyChanged { get; init; }
    public Action? OnCategoryImported { get; init; }

    private UserCategoryDefinition _categoryDefinition = new();

    private readonly HorizontalListNode _headerButtonsList;
    private readonly ScrollingAreaNode<VerticalListNode> _scrollingArea;
    private readonly List<ConfigurationSection> _sections = new();

    public CategoryDefinitionConfigurationNode()
    {
        _headerButtonsList = new HorizontalListNode
        {
            Height = 30,
            ItemSpacing = 2.0f,
            Alignment = HorizontalListAnchor.Right,
        };
        _headerButtonsList.AttachNode(this);

        _headerButtonsList.AddNode(new ResNode { Width = 12 });

        _headerButtonsList.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Services.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\download.png"),
            TextTooltip = "Import Category from Clipboard (Overwrites current)\n(hold shift to confirm)",
            OnClick = HandleImportCategory,
        });

        _headerButtonsList.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Services.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\upload.png"),
            TextTooltip = "Export Category to Clipboard",
            OnClick = HandleExportCategory,
        });

        _scrollingArea = new ScrollingAreaNode<VerticalListNode> {
            AutoHideScrollBar = true,
            ContentHeight = 100f
        };
        _scrollingArea.AttachNode(this);

        var list = _scrollingArea.ContentAreaNode;
        list.FitContents = true;
        list.ItemSpacing = 4.0f;

        _sections.Add(new BasicSettingsSection(() => _categoryDefinition) {
            String = "Basic Settings", IsCollapsed = false,
            OnPropertyChanged = () => { NotifyChanged(); OnCategoryPropertyChanged?.Invoke(); }
        });

        _sections.Add(new RangeFiltersSection(() => _categoryDefinition) { String = "Range Filters" });
        _sections.Add(new StateFiltersSection(() => _categoryDefinition) { String = "State Filters" });
        _sections.Add(new ListFiltersSection(() => _categoryDefinition) {
            String = "List Filters",
            OnListChanged = HandleLayoutChange
        });

        foreach (var section in _sections)
        {
            section.OnToggle = HandleLayoutChange;
            section.OnValueChanged = NotifyChanged;
            list.AddNode(section);
        }
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        _headerButtonsList.Size = new Vector2(Width, 30);
        _headerButtonsList.Position = new Vector2(0, 0);
        _headerButtonsList.RecalculateLayout();

        _scrollingArea.Position = new Vector2(0, 34);
        _scrollingArea.Size = Size with { Y = Size.Y - 34 };

        foreach (var section in _sections)
        {
            section.Width = Width - 16.0f;
        }
        HandleLayoutChange();
    }

    public void SetCategory(UserCategoryDefinition newCategory)
    {
        _categoryDefinition = newCategory;
        foreach (var section in _sections) section.Refresh();
        HandleLayoutChange();
    }

    private void HandleLayoutChange()
    {
        _scrollingArea.ContentAreaNode.RecalculateLayout();
        _scrollingArea.ContentHeight = _scrollingArea.ContentAreaNode.Height;
        OnLayoutChanged?.Invoke();
    }

    private static void NotifyChanged() => InventoryOrchestrator.RefreshAll(updateMaps: true);

    private void HandleExportCategory()
    {
        CategoryImportExport.ExportCategoryToClipboard(_categoryDefinition);
    }

    private void HandleImportCategory()
    {
        if (!Services.KeyState[VirtualKey.SHIFT]) return;

        var imported = CategoryImportExport.ImportCategoryFromClipboard();
        if (imported is null) return;

        _categoryDefinition.Name = imported.Name;
        _categoryDefinition.Description = imported.Description;
        _categoryDefinition.Priority = imported.Priority;
        _categoryDefinition.Color = imported.Color;
        _categoryDefinition.Enabled = imported.Enabled;
        _categoryDefinition.Pinned = imported.Pinned;
        _categoryDefinition.Rules = imported.Rules;

        Util.SaveConfig(System.Config);

        foreach (var section in _sections) section.Refresh();
        HandleLayoutChange();
        OnCategoryPropertyChanged?.Invoke();
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    public static string ResolveItemName(uint itemId) => ItemSheet?.GetRow(itemId).Name.ToString() ?? "Unknown";

    public static string ResolveUiCategoryName(uint categoryId) => UICategorySheet?.GetRow(categoryId).Name.ToString() ?? "Unknown";
}

public abstract class ConfigurationSection : CollapsibleSectionNode
{
    private readonly Func<UserCategoryDefinition> _getCategoryDefinition;

    public Action? OnValueChanged { get; set; }

    protected UserCategoryDefinition CategoryDefinition => _getCategoryDefinition();

    protected ConfigurationSection(Func<UserCategoryDefinition> getCategoryDefinition)
    {
        _getCategoryDefinition = getCategoryDefinition;
        HeaderHeight = 30.0f;

        AddTab();
    }

    public abstract void Refresh();

    protected static LabelTextNode CreateLabel(string text) => new()
    {
        TextFlags = TextFlags.AutoAdjustNodeSize,
        Size = new Vector2(80, 20),
        String = text,
    };
}