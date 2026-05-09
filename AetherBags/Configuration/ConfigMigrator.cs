using AetherBags.Configuration.Migrations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AetherBags.Configuration;

public static class ConfigMigrator
{
    public const int CurrentVersion = 1;

    private static readonly IConfigMigration[] Migrations =
    [
        new V1ItemSortSettingsMigration(),
    ];

    public static string Migrate(string json, out bool migrated)
    {
        migrated = false;

        if (string.IsNullOrWhiteSpace(json))
            return json;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch
        {
            return json;
        }

        if (node is not JsonObject root)
            return json;

        int version = JsonMigrationHelpers.GetInt(root["Version"]) ?? 0;

        foreach (var migration in Migrations)
        {
            if (version >= migration.Version)
                continue;

            migration.Apply(root);
            version = migration.Version;
            root["Version"] = version;
            migrated = true;
        }

        return migrated
            ? root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            : json;
    }
}
