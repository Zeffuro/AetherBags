using KamiToolKit.Premade.GenericListItemNodes;

namespace AetherBags.Addons;

public class CategoryListItemNode : GenericListItemNode<CategoryWrapper>
{
    protected override uint GetIconId(CategoryWrapper data) => data.GetIconId() ?? 0;

    protected override string GetLabelText(CategoryWrapper data) => data.GetLabel();

    protected override string GetSubLabelText(CategoryWrapper data) => data.GetSubLabel();

    protected override uint? GetId(CategoryWrapper data) => data.GetId();
}