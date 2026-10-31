using System;
using System.Collections.Generic;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Interfaces;

namespace AetherBags.Nodes.Layout;

internal sealed class LayoutVisibilityScope : IDisposable
{
    private const DrawFlags DrawDisabled = (DrawFlags)0x40000;
    private readonly List<NodeBase> culledNodes = [];

    public LayoutVisibilityScope(NodeBase root) => Include(root);

    private void Include(NodeBase node)
    {
        if ((node.DrawFlags & DrawDisabled) != 0)
        {
            culledNodes.Add(node);
            node.DrawFlags &= ~DrawDisabled;
        }

        if (node is ILayoutListNode layout)
            foreach (var child in layout.Nodes) Include(child);
    }

    public void Dispose()
    {
        foreach (var node in culledNodes)
            node.DrawFlags |= DrawDisabled;
    }
}
