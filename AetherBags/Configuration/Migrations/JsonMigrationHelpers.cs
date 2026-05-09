using System.Text.Json;
using System.Text.Json.Nodes;

namespace AetherBags.Configuration.Migrations;

internal static class JsonMigrationHelpers
{
    public static int? GetInt(JsonNode? node)
    {
        if (node is null)
            return null;

        try
        {
            return node.GetValueKind() switch
            {
                JsonValueKind.Number => node.GetValue<int>(),
                JsonValueKind.String when int.TryParse(node.GetValue<string>(), out int value) => value,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }
}
