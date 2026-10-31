using System;
using AetherBags.Nodes.Layout;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration;

public class ConfigurationScrollingNode : ScrollingNode<VerticalListNode>
{
    public ConfigurationScrollingNode() => ContentNode.FitContents = true;

    public void RefreshLayout()
    {
        if (IsDisposed) return;
        RecalculateSizes();
    }

    protected override void OnSizeChanged()
    {
        using var visibility = new LayoutVisibilityScope(ContentNode);
        var scrollPosition = ScrollBarNode.ScrollPosition;
        base.OnSizeChanged();
        ContentNode.RecalculateLayout();
        ScrollBarNode.UpdateScrollParams();
        ScrollBarNode.ScrollPosition = Math.Clamp(scrollPosition, 0, ScrollBarNode.ScrollMaxPosition);
    }
}
