using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AetherBags.Tags;

public static class DtrTagEngine
{
    private static readonly Regex TagRegex = new(
        @"\[(?<key>[^:\]\[_.]+(?:\.(?!\d+\])[^:\]\[_.]+)?)(?:_(?<subKey>[^:\]\.]+))?(?::(?<format>[^\]\.]+))?(?:\.(?<precision>\d+))?\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, List<ParsedTag>> ParseCache = new();

    public static string Process(string input, DtrTagContext context)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        if (!ParseCache.TryGetValue(input, out var segments))
        {
            segments = ParseTemplate(input);
            ParseCache[input] = segments;
        }

        if (segments.Count == 1)
            return segments[0].IsLiteral ? segments[0].LiteralText : ResolveTag(segments[0], context);

        var builder = new StringBuilder();
        foreach (var segment in segments)
            builder.Append(segment.IsLiteral ? segment.LiteralText : ResolveTag(segment, context));

        return builder.ToString();
    }

    public static void ClearCache()
    {
        ParseCache.Clear();
    }

    private static List<ParsedTag> ParseTemplate(string input)
    {
        var segments = new List<ParsedTag>();
        var matches = TagRegex.Matches(input);
        var lastEnd = 0;

        foreach (Match match in matches)
        {
            if (match.Index > lastEnd)
                segments.Add(ParsedTag.Literal(input[lastEnd..match.Index]));

            var key = match.Groups["key"].Value;
            var subKey = match.Groups["subKey"].Value;
            var format = match.Groups["format"].Value;
            var precisionText = match.Groups["precision"].Value;
            int? precision = string.IsNullOrEmpty(precisionText) ? null : int.Parse(precisionText);

            segments.Add(ParsedTag.Tag(key, subKey, format, precision, match.Value));
            lastEnd = match.Index + match.Length;
        }

        if (lastEnd < input.Length)
            segments.Add(ParsedTag.Literal(input[lastEnd..]));

        if (segments.Count == 0)
            segments.Add(ParsedTag.Literal(input));

        return segments;
    }

    private static string ResolveTag(ParsedTag tag, DtrTagContext context)
    {
        return context.TryGetValue(tag.Key, tag.SubKey, out var value)
            ? NumericTagFormatter.Format(value, tag.Format, tag.Precision)
            : tag.OriginalText;
    }
}
