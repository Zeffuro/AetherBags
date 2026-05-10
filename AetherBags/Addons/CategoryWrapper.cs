using AetherBags.Configuration;
using AetherBags.Inventory.Categories;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public enum CategoryWrapperKind { User, Override, GameCategory }

public sealed class CategoryWrapper
{
    public CategoryWrapperKind Kind { get; }
    public UserCategoryDefinition? CategoryDefinition { get; }
    public uint? GameCategoryId { get; }

    private CategoryWrapper(CategoryWrapperKind kind, UserCategoryDefinition? def, uint? gameId)
    {
        Kind = kind;
        CategoryDefinition = def;
        GameCategoryId = gameId;
    }

    public static CategoryWrapper ForUserOrOverride(UserCategoryDefinition def)
    {
        var kind = def.OverrideSourceKey is null ? CategoryWrapperKind.User : CategoryWrapperKind.Override;
        return new CategoryWrapper(kind, def, null);
    }

    public static CategoryWrapper ForGameCategory(uint id)
        => new(CategoryWrapperKind.GameCategory, null, id);

    public string GetLabel() => Kind switch
    {
        CategoryWrapperKind.GameCategory => ResolveGameCategoryName(GameCategoryId!.Value),
        _ => CategoryDefinition!.Name,
    };

    public string GetSubLabel() => Kind switch
    {
        CategoryWrapperKind.User => UserCategoryMatcher.IsCatchAll(CategoryDefinition!)
            ? " No valid rules!"
            : (CategoryDefinition!.Enabled ? "✓ User · Enabled" : " User · Disabled"),
        CategoryWrapperKind.Override => $"⊕ Overrides: {ResolveOverrideSourceName(CategoryDefinition!.OverrideSourceKey!)}",
        CategoryWrapperKind.GameCategory => "▤ Game Category",
        _ => string.Empty,
    };

    public uint? GetId() => null;

    public uint? GetIconId() => 0;

    public int Compare(CategoryWrapper other)
    {
        int kindCmp = ((int)Kind).CompareTo((int)other.Kind);
        if (kindCmp != 0) return kindCmp;

        return Kind switch
        {
            CategoryWrapperKind.GameCategory => GameCategoryId!.Value.CompareTo(other.GameCategoryId!.Value),
            _ => CategoryDefinition!.Order.CompareTo(other.CategoryDefinition!.Order),
        };
    }

    public static string ResolveGameCategoryName(uint id)
    {
        var sheet = Services.DataManager.GetExcelSheet<ItemUICategory>();
        if (sheet.TryGetRow(id, out var row))
        {
            var name = row.Name.ToString();
            return string.IsNullOrWhiteSpace(name) ? $"Game Category {id}" : name;
        }
        return $"Game Category {id}";
    }

    public static string ResolveOverrideSourceName(string overrideSourceKey)
    {
        if (CategoryOverrideKey.TryParseGameCategory(overrideSourceKey, out var gameId))
            return ResolveGameCategoryName(gameId);

        return overrideSourceKey;
    }
}
