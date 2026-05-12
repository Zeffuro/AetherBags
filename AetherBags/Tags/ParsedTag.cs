namespace AetherBags.Tags;

public readonly struct ParsedTag
{
    public bool IsLiteral { get; init; }
    public string LiteralText { get; init; }
    public string Key { get; init; }
    public string SubKey { get; init; }
    public string Format { get; init; }
    public int? Precision { get; init; }
    public string OriginalText { get; init; }

    public static ParsedTag Literal(string text) => new()
    {
        IsLiteral = true,
        LiteralText = text,
        Key = string.Empty,
        SubKey = string.Empty,
        Format = string.Empty,
        Precision = null,
        OriginalText = text,
    };

    public static ParsedTag Tag(string key, string subKey, string format, int? precision, string originalText) => new()
    {
        IsLiteral = false,
        LiteralText = string.Empty,
        Key = key,
        SubKey = subKey,
        Format = format,
        Precision = precision,
        OriginalText = originalText,
    };
}
