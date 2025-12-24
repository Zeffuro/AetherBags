using KamiToolKit;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

public class HybridDirectionalStackNode<T> : LayoutListNode where T : NodeBase
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

    public bool Vertical
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            RecalculateLayout();
        }
    } = true;

    public float Spacing
    {
        get => field;
        set
        {
            if (field.Equals(value)) return;
            field = value;
            RecalculateLayout();
        }
    } = 1f;

    public bool StretchCrossAxis
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            RecalculateLayout();
        }
    } = true;

    protected override void InternalRecalculateLayout()
    {
        int count = NodeList.Count;
        if (count == 0) return;

        FlexGrowDirection dir = GrowDirection;
        bool alignRight = dir == FlexGrowDirection.DownLeft || dir == FlexGrowDirection.UpLeft;
        bool alignBottom = dir == FlexGrowDirection.UpRight || dir == FlexGrowDirection.UpLeft;

        bool vertical = Vertical;
        bool stretchCross = StretchCrossAxis;

        float containerW = Width;
        float containerH = Height;

        float startX = alignRight ? containerW : 0f;
        float startY = alignBottom ? containerH : 0f;

        float spacing = Spacing;

        float cursor = 0f;

        if (vertical)
        {
            for (int i = 0; i < count; i++)
            {
                NodeBase node = NodeList[i];

                if (stretchCross)
                    node.Width = containerW;

                float w = node.Width;
                float h = node.Height;

                node.X = alignRight ? startX - w : startX;
                node.Y = alignBottom ? startY - h - cursor : startY + cursor;

                AdjustNode(node);

                cursor += node.Height + spacing;
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                NodeBase node = NodeList[i];

                if (stretchCross)
                    node.Height = containerH;

                float w = node.Width;
                float h = node.Height;

                node.X = alignRight ? startX - w - cursor : startX + cursor;
                node.Y = alignBottom ? startY - h : startY;

                AdjustNode(node);

                cursor += node.Width + spacing;
            }
        }
    }
}