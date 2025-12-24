using KamiToolKit;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

public class HybridDirectionalFlexNode : HybridDirectionalFlexNode<NodeBase> { }

public class HybridDirectionalFlexNode<T> : LayoutListNode where T : NodeBase
{
    public FlexGrowDirection GrowDirection
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            RecalculateLayout();
        }
    } = FlexGrowDirection.DownRight;

    public int ItemsPerLine
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            RecalculateLayout();
        }
    } = 1;

    public bool FillRowsFirst
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            RecalculateLayout();
        }
    } = true;

    public float HorizontalPadding
    {
        get => field;
        set
        {
            if (field.Equals(value)) return;
            field = value;
            RecalculateLayout();
        }
    } = 1;

    public float VerticalPadding
    {
        get => field;
        set
        {
            if (field.Equals(value)) return;
            field = value;
            RecalculateLayout();
        }
    } = 1;

    protected override void InternalRecalculateLayout()
    {
        int count = NodeList.Count;
        if (count == 0) return;

        int itemsPerLine = ItemsPerLine;
        if (itemsPerLine < 1) itemsPerLine = 1;

        NodeBase first = NodeList[0];
        float nodeWidth = first.Width;
        float nodeHeight = first.Height;

        float hPad = HorizontalPadding;
        float vPad = VerticalPadding;

        FlexGrowDirection dir = GrowDirection;
        bool alignRight = dir == FlexGrowDirection.DownLeft || dir == FlexGrowDirection.UpLeft;
        bool alignBottom = dir == FlexGrowDirection.UpRight || dir == FlexGrowDirection.UpLeft;

        float startX = alignRight ? Width : 0f;
        float startY = alignBottom ? Height : 0f;

        float stepX = nodeWidth + hPad;
        float stepY = nodeHeight + vPad;

        bool fillRowsFirst = FillRowsFirst;

        int major = 0;
        int minor = 0;

        for (int i = 0; i < count; i++)
        {
            int row, col;
            if (fillRowsFirst)
            {
                row = major;
                col = minor;
            }
            else
            {
                col = major;
                row = minor;
            }

            float x = alignRight
                ? startX - nodeWidth - col * stepX
                : startX + col * stepX;

            float y = alignBottom
                ? startY - nodeHeight - row * stepY
                : startY + row * stepY;

            NodeBase node = NodeList[i];
            node.X = x;
            node.Y = y;

            AdjustNode(node);

            minor++;
            if (minor == itemsPerLine)
            {
                minor = 0;
                major++;
            }
        }
    }
}