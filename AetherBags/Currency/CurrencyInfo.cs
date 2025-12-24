namespace AetherBags.Currency;

public class CurrencyInfo
{
    public required uint Amount { get; set; }
    public required uint MaxAmount { get; set; }
    public required uint ItemId { get; set; }
    public required uint IconId { get; set; }
    public required bool LimitReached { get; set; }
    public required bool IsCapped { get; set; }
}