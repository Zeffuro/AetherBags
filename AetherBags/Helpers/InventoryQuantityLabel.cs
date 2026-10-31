using KamiToolKit.Nodes;

namespace AetherBags.Helpers;

internal static class InventoryQuantityLabel
{
    public static void Update(TextNode node, int count, bool showCount)
    {
        node.FontSize = 12;
        node.String = !showCount ? string.Empty
            : count < 100_000 ? count.ToString() : NumberFormatter.FormatCompact(count);

        if (!showCount)
            return;

        while (node.FontSize > 8 && node.GetTextDrawSize(considerScale: false).X > node.Width)
            node.FontSize--;
    }
}
