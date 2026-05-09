using System.Text.Json.Nodes;

namespace AetherBags.Configuration.Migrations;

internal interface IConfigMigration
{
    int Version { get; }

    void Apply(JsonObject root);
}
