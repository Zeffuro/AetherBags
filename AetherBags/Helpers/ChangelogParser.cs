using System.Collections.Generic;
using System.IO;

namespace AetherBags.Helpers;

public static class ChangelogParser
{
    public static IReadOnlyList<ChangelogVersion> Load()
    {
        var path = GetChangelogPath();
        if (!File.Exists(path))
            return [new ChangelogVersion("Changelog unavailable", ["Unable to find changelog.md."])];

        return Parse(File.ReadAllLines(path));
    }

    private static IReadOnlyList<ChangelogVersion> Parse(IEnumerable<string> lines)
    {
        var versions = new List<ChangelogVersion>();
        string? currentVersion = null;
        var currentItems = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('#'))
            {
                AddCurrent();
                currentVersion = line.TrimStart('#').Trim();
                currentItems = [];
                continue;
            }

            if (line.StartsWith('-'))
            {
                if (currentVersion == null)
                    currentVersion = "Unreleased";

                currentItems.Add(line.TrimStart('-').Trim());
            }
        }

        AddCurrent();
        return versions;

        void AddCurrent()
        {
            if (currentVersion == null) return;
            versions.Add(new ChangelogVersion(currentVersion, currentItems.ToArray()));
        }
    }

    private static string GetChangelogPath()
    {
        var baseDir = Services.PluginInterface.AssemblyLocation.Directory!.FullName;
        return Path.Combine(baseDir, "changelog.md");
    }
}

public sealed class ChangelogVersion(string version, IReadOnlyList<string> items)
{
    public readonly string Version = version;
    public readonly IReadOnlyList<string> Items = items;
}


