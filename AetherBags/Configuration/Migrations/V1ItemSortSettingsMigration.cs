using System.Text.Json;
using System.Text.Json.Nodes;

namespace AetherBags.Configuration.Migrations;

internal sealed class V1ItemSortSettingsMigration : IConfigMigration
{
    public int Version => 1;

    public void Apply(JsonObject root)
    {
        if (root["Categories"] is not JsonObject categories)
            return;

        if (!HasCriteria(categories["DefaultItemSortCriteria"]))
            categories["DefaultItemSortCriteria"] = CreateCriteriaArray(categories["DefaultItemSortMode"], allowUseGlobal: false);

        categories.Remove("DefaultItemSortMode");

        if (categories["UserCategories"] is not JsonArray userCategories)
            return;

        foreach (var categoryNode in userCategories)
        {
            if (categoryNode is not JsonObject category)
                continue;

            if (!HasCriteria(category["ItemSortCriteria"]))
                category["ItemSortCriteria"] = CreateCriteriaArray(category["ItemSortMode"], allowUseGlobal: true);

            category.Remove("ItemSortMode");
        }
    }

    private static bool HasCriteria(JsonNode? node)
        => node is JsonArray { Count: > 0 };

    private static JsonArray CreateCriteriaArray(JsonNode? legacyModeNode, bool allowUseGlobal)
    {
        var (field, direction) = MapLegacyMode(GetLegacyMode(legacyModeNode), allowUseGlobal);
        return
        [
            new JsonObject
            {
                ["Field"] = (int)field,
                ["Direction"] = (int)direction,
            }
        ];
    }

    private static int? GetLegacyMode(JsonNode? node)
    {
        if (node is null)
            return null;

        if (JsonMigrationHelpers.GetInt(node) is { } intValue)
            return intValue;

        var stringValue = node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
        return stringValue switch
        {
            "UseGlobal" => 0,
            "QuantityDescending" => 1,
            "NameAscending" => 2,
            "RarityDescending" => 3,
            "RarityAscending" => 4,
            "ItemIdAscending" => 5,
            "ItemIdDescending" => 6,
            "CustomOrder" => 7,
            "GameCategory" => 8,
            "ItemLevelAscending" => 9,
            "ItemLevelDescending" => 10,
            _ => null,
        };
    }

    private static (ItemSortField Field, SortDirection Direction) MapLegacyMode(int? mode, bool allowUseGlobal) => mode switch
    {
        0 when allowUseGlobal => (ItemSortField.UseGlobal, SortDirection.Ascending),
        2 => (ItemSortField.Name, SortDirection.Ascending),
        3 => (ItemSortField.Rarity, SortDirection.Descending),
        4 => (ItemSortField.Rarity, SortDirection.Ascending),
        5 => (ItemSortField.ItemId, SortDirection.Ascending),
        6 => (ItemSortField.ItemId, SortDirection.Descending),
        7 => (ItemSortField.CustomOrder, SortDirection.Ascending),
        8 => (ItemSortField.GameCategory, SortDirection.Ascending),
        9 => (ItemSortField.ItemLevel, SortDirection.Ascending),
        10 => (ItemSortField.ItemLevel, SortDirection.Descending),
        _ => (ItemSortField.Quantity, SortDirection.Descending),
    };
}
