using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AetherBags.Helpers;

/// <summary>
/// Thread-safe cache for compiled Regex objects to avoid repeated compilation overhead.
/// </summary>
internal static class RegexCache
{
    private const int MaxCacheSize = 128;
    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    /// <summary>
    /// Gets or creates a compiled Regex for the given pattern with case-insensitive matching.
    /// Returns null if the pattern is invalid.
    /// </summary>
    public static Regex? GetOrCreate(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return null;

        if (Cache.TryGetValue(pattern, out var cached))
            return cached;

        try
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (Cache.Count < MaxCacheSize)
            {
                Cache.TryAdd(pattern, regex);
            }

            return regex;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the regex cache. Call when configuration changes significantly.
    /// </summary>
    public static void Clear() => Cache.Clear();
}
