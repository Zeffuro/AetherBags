namespace AetherBags.Configuration;

public class DtrSettings
{
    public const string DefaultFormatString = "Bags [used]/[total] ([percent:0]%)";

    public bool Enabled { get; set; } = true;
    public string FormatString { get; set; } = DefaultFormatString;
}
