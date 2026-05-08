using KamiToolKit.Premade.Node.ListItem;

namespace AetherBags.Addons;

public class CategoryListItemNode : IconListItemNode<CategoryWrapper>
{
    protected override uint GetIconId(CategoryWrapper data) => data.GetIconId() ?? 0;

    protected override string GetLabelText(CategoryWrapper data) => data.GetLabel();

    protected override string GetSubLabelText(CategoryWrapper data) => data.GetSubLabel();

    protected override uint? GetId(CategoryWrapper data) => data.GetId();
}