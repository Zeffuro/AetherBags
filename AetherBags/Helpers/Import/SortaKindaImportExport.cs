using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AetherBags.Configuration;
using AetherBags.Configuration.Import;

namespace AetherBags.Helpers.Import;

public static class SortaKindaImportExport
{
    private static readonly JsonSerializerOptions ExternalJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        IncludeFields = true
    };

    public static bool TryImportFromClipboard(
        SystemConfiguration targetConfig,
        bool replaceExisting,
        out string error)
    {
        error = string.Empty;
        string clipboard;
        try
        {
            clipboard = Dalamud.Bindings.ImGui.ImGui.GetClipboardText();
        }
        catch (Exception ex)
        {
            error = $"Failed to read clipboard: {ex.Message}";
            return false;
        }

        return TryImportFromJson(clipboard, targetConfig, replaceExisting, out error);
    }

    public static bool TryImportFromJson(
        string input,
        SystemConfiguration targetConfig,
        bool replaceExisting,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Input was empty.";
            return false;
        }

        string trimmed = input.Trim();

        SortaKindaCategory[]? external = null;

        SortaKindaImportFile? file = Util.DeserializeCompressed<SortaKindaImportFile>(trimmed, ExternalJsonOptions);
        if (file?.Rules is { Count: > 0 })
        {
            external = file.Rules.ToArray();
        }
        else
        {
            external = Util.DeserializeCompressed<SortaKindaCategory[]>(trimmed, ExternalJsonOptions);
        }

        if (external is null)
        {
            error = "Failed to parse SortaKinda input.";
            return false;
        }

        var mapped = external
            .Select(MapToUserCategory)
            .OrderBy(c => c.Order)
            .ToList();

        var dest = targetConfig.Categories.UserCategories;

        if (replaceExisting)
        {
            dest.Clear();
            dest.AddRange(mapped);
        }
        else
        {
            var byId = dest
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var incoming in mapped)
            {
                if (!string.IsNullOrWhiteSpace(incoming.Id) && byId.TryGetValue(incoming.Id, out var existing))
                {
                    existing.Name = incoming.Name;
                    existing.Description = incoming.Description;
                    existing.Order = incoming.Order;
                    existing.Priority = incoming.Priority;
                    existing.Color = incoming.Color;
                    existing.Rules = incoming.Rules;
                }
                else
                {
                    dest.Add(incoming);
                    if (!string.IsNullOrWhiteSpace(incoming.Id))
                        byId[incoming.Id] = incoming;
                }
            }
        }

        targetConfig.Categories.UserCategoriesEnabled = true;
        return true;
    }

    public static string ExportToJson(SystemConfiguration sourceConfig)
    {
        var exported = new SortaKindaImportFile
        {
            Rules = sourceConfig.Categories.UserCategories
                .OrderBy(c => c.Priority)
                .Select(MapToExternal)
                .ToList(),

            // MainInventory = new { InventoryConfigs = new[] { new { } } }
        };

        return Util.SerializeCompressed(exported, ExternalJsonOptions);
    }

    public static void ExportToClipboard(SystemConfiguration sourceConfig)
        => Dalamud.Bindings.ImGui.ImGui.SetClipboardText(ExportToJson(sourceConfig));

    private static UserCategoryDefinition MapToUserCategory(SortaKindaCategory external)
        => new()
        {
            Id = string.IsNullOrWhiteSpace(external.Id) ? Guid.NewGuid().ToString("N") : external.Id,
            Name = external.Name,
            Description = string.Empty,
            Order = external.Index,
            Priority = external.Index,
            Color = external.Color,
            Rules = new CategoryRuleSet
            {
                AllowedItemIds = new List<uint>(),

                AllowedItemNamePatterns =
                    (external.AllowedItemNames ?? new List<string>())
                    .Concat((external.AllowedNameRegexes ?? new List<AllowedNameRegexDto>())
                        .Select(r => r.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t)))
                    .ToList(),

                AllowedUiCategoryIds = external.AllowedItemTypes?.ToList() ?? new List<uint>(),
                AllowedRarities = external.AllowedItemRarities?.ToList() ?? new List<int>(),

                Level = new RangeFilter<int>
                {
                    Enabled = external.LevelFilter?.Enable ?? false,
                    Min = external.LevelFilter?.MinValue ?? 0,
                    Max = external.LevelFilter?.MaxValue ?? 200,
                },
                ItemLevel = new RangeFilter<int>
                {
                    Enabled = external.ItemLevelFilter?.Enable ?? false,
                    Min = external.ItemLevelFilter?.MinValue ?? 0,
                    Max = external.ItemLevelFilter?.MaxValue ?? 2000,
                },
                VendorPrice = new RangeFilter<uint>
                {
                    Enabled = external.VendorPriceFilter?.Enable ?? false,
                    Min = external.VendorPriceFilter?.MinValue ?? 0u,
                    Max = external.VendorPriceFilter?.MaxValue ?? 9_999_999u,
                },

                Untradable = new StateFilter { State = external.UntradableFilter?.State ?? 0, Filter = external.UntradableFilter?.Filter ?? 0 },
                Unique     = new StateFilter { State = external.UniqueFilter?.State ?? 0,     Filter = external.UniqueFilter?.Filter ?? 0 },
                Collectable= new StateFilter { State = external.CollectableFilter?.State ?? 0,Filter = external.CollectableFilter?.Filter ?? 0 },
                Dyeable    = new StateFilter { State = external.DyeableFilter?.State ?? 0,    Filter = external.DyeableFilter?.Filter ?? 0 },
                Repairable = new StateFilter { State = external.RepairableFilter?.State ?? 0, Filter = external.RepairableFilter?.Filter ?? 0 },
            }
        };

    private static SortaKindaCategory MapToExternal(UserCategoryDefinition internalCat)
        => new()
        {
            Color = internalCat.Color,
            Id = internalCat.Id,
            Name = internalCat.Name,
            Index = internalCat.Priority,

            AllowedItemNames = new List<string>(),
            AllowedNameRegexes =
                (internalCat.Rules.AllowedItemNamePatterns ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new AllowedNameRegexDto { Text = s })
                .ToList(),

            AllowedItemTypes = internalCat.Rules.AllowedUiCategoryIds?.ToList() ?? new List<uint>(),
            AllowedItemRarities = internalCat.Rules.AllowedRarities?.ToList() ?? new List<int>(),

            LevelFilter = new ExternalRangeFilterDto<int>
            {
                Enable = internalCat.Rules.Level.Enabled,
                Label = "Level Filter",
                MinValue = internalCat.Rules.Level.Min,
                MaxValue = internalCat.Rules.Level.Max
            },

            ItemLevelFilter = new ExternalRangeFilterDto<int>
            {
                Enable = internalCat.Rules.ItemLevel.Enabled,
                Label = "Item Level Filter",
                MinValue = internalCat.Rules.ItemLevel.Min,
                MaxValue = internalCat.Rules.ItemLevel.Max
            },
            VendorPriceFilter = new ExternalRangeFilterDto<uint>
            {
                Enable = internalCat.Rules.VendorPrice.Enabled,
                Label = "Vendor Price Filter",
                MinValue = internalCat.Rules.VendorPrice.Min,
                MaxValue = internalCat.Rules.VendorPrice.Max
            },

            UntradableFilter = new ExternalStateFilterDto { State = internalCat.Rules.Untradable.State, Filter = internalCat.Rules.Untradable.Filter },
            UniqueFilter     = new ExternalStateFilterDto { State = internalCat.Rules.Unique.State,     Filter = internalCat.Rules.Unique.Filter },
            CollectableFilter= new ExternalStateFilterDto { State = internalCat.Rules.Collectable.State,Filter = internalCat.Rules.Collectable.Filter },
            DyeableFilter    = new ExternalStateFilterDto { State = internalCat.Rules.Dyeable.State,    Filter = internalCat.Rules.Dyeable.Filter },
            RepairableFilter = new ExternalStateFilterDto { State = internalCat.Rules.Repairable.State, Filter = internalCat.Rules.Repairable.Filter },

            Direction = 0,
            FillMode = 0,
            SortMode = 0,
            InclusiveAnd = false,
        };
}