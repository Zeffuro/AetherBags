using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Addons;

public sealed class AddonChangelogWindow : NativeAddon
{
    private ScrollingListNode? _scrollingAreaNode;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        _scrollingAreaNode = new ScrollingListNode
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            ItemSpacing = 4,
            FitWidth = true,
            IsVisible = true,
        };
        _scrollingAreaNode.AttachNode(this);

        PopulateChangelog(_scrollingAreaNode);
        _scrollingAreaNode.RecalculateLayout();

        base.OnSetup(addon, atkValueSpan);
    }

    private static void PopulateChangelog(ScrollingListNode listNode)
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

