using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public sealed class AddonChangelogWindow : NativeAddon
{
    private ScrollingNode<VerticalListNode>? _scrollingAreaNode;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        _scrollingAreaNode = new ScrollingNode<VerticalListNode>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            ContentNode = { ItemSpacing = 4, FitWidth = true, FitContents = true },
            IsVisible = true,
        };
        _scrollingAreaNode.AttachNode(this);

        PopulateChangelog(_scrollingAreaNode.ContentNode);
        _scrollingAreaNode.RecalculateSizes();

        base.OnSetup(addon, atkValueSpan);
    }

    private static void PopulateChangelog(VerticalListNode listNode)
    {
        foreach (var version in ChangelogParser.Load())
        {
            listNode.AddNode(new CategoryTextNode
            {
                Height = 20,
                String = version.Version,
            });

            foreach (var item in version.Items)
            {
                foreach (var line in WrapBullet(item))
                {
                    listNode.AddNode(new TextNode
                    {
                        Size = new Vector2(560, 18),
                        String = line,
                    });
                }
            }

            listNode.AddNode(new ResNode { Height = 8 });
        }
    }

    private static IEnumerable<string> WrapBullet(string text)
    {
        const int maxLineLength = 92;
        var remaining = "- " + text;

        while (remaining.Length > maxLineLength)
        {
            var splitIndex = remaining.LastIndexOf(' ', maxLineLength);
            if (splitIndex <= 0) splitIndex = maxLineLength;

            yield return remaining[..splitIndex];
            remaining = "  " + remaining[splitIndex..].TrimStart();
        }

        yield return remaining;
    }
}

