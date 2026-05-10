using System;

namespace AetherBags.Helpers;

public static class NumberFormatter
{
    private static readonly string[] CompactSuffixes = { "", "K", "M", "B", "T" };

    public static string FormatCompact(double value)
    {
        if (Math.Abs(value) < 10_000) return value.ToString("N0");

        double working = value;
        int index = 0;
        while (Math.Abs(working) >= 1000 && index < CompactSuffixes.Length - 1)
        {
            working /= 1000;
            index++;
        }

        // Truncate so 999.999 doesn't round into the next bucket.
        working = Math.Truncate(working * 10) / 10;

        return working % 1 == 0
            ? $"{working:N0}{CompactSuffixes[index]}"
            : $"{working:N1}{CompactSuffixes[index]}";
    }
}
