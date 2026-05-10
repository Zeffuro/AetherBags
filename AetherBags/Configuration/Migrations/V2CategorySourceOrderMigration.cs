using System.Text.Json;
using System.Text.Json.Nodes;

namespace AetherBags.Configuration.Migrations;

internal sealed class V2CategorySourceOrderMigration : IConfigMigration
{
    public int Version => 2;

    public void Apply(JsonObject root)
    {
        if (root["Categories"] is not JsonObject categories) return;
        if (categories["CategorySourceDisplayOrder"] is not JsonArray order) return;

        var converted = new JsonArray();
        foreach (var entry in order)
        {
            string? id = entry switch
            {
                null => null,
                JsonValue value when value.GetValueKind() == JsonValueKind.Number
                    => MapEnumIndex(value.GetValue<int>()),
                JsonValue value when value.GetValueKind() == JsonValueKind.String
                    => value.GetValue<string>(),
                _ => null,
            };

            if (id is not null) converted.Add(id);
        }

        categories["CategorySourceDisplayOrder"] = converted;
    }

    private static string? MapEnumIndex(int index) => index switch
    {
        0 => CategorySourceIds.UserCategories,
        1 => CategorySourceIds.BisBuddy,
        2 => CategorySourceIds.AllaganTools,
        3 => CategorySourceIds.GameCategories,
        4 => CategorySourceIds.Misc,
        _ => null,
    };
}
