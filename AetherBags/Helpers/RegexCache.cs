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
    private static readonly ConcurrentQueue<string> Order = new();

    /// <summary>
    /// Gets or creates a Regex for the given pattern with case-insensitive matching.
    /// Returns null if the pattern is invalid.
    /// The 'compiled' parameter controls whether the Regex is compiled for faster execution at the cost of longer initial compilation time and higher memory usage.
    /// Use with caution and consider the expected usage patterns.
    /// </summary>
    public static Regex? GetOrCreate(string pattern, bool compiled = false)
    {
        if (string.IsNullOrEmpty(pattern))
            return null;

        string key = pattern + '\0' + (compiled ? "C" : "I");

        if (Cache.TryGetValue(key, out var cached))
            return cached;

        try
        {
            var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
            if (compiled)
                options |= RegexOptions.Compiled;

            var regex = new Regex(pattern, options);

            if (Cache.TryAdd(key, regex))
            {
                Order.Enqueue(key);

                while (Cache.Count > MaxCacheSize && Order.TryDequeue(out var oldest))
                {
                    Cache.TryRemove(oldest, out _);
                }
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
