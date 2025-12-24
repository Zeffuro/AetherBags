using System;
using System.Numerics;
using KamiToolKit.Nodes;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Input;

public class TextInputWithHintNode : SimpleComponentNode {
    private readonly TextInputNode _textInputNode;
    private readonly ImageNode _helpNode;

    public TextInputWithHintNode() {
        _textInputNode = new TextInputNode {
            PlaceholderString = "Search . . .",
        };
        _textInputNode.AttachNode(this);

        _helpNode = new SimpleImageNode {
            TexturePath = "ui/uld/CircleButtons.tex",
            TextureCoordinates = new Vector2(112.0f, 84.0f),
            TextureSize = new Vector2(28.0f, 28.0f),
            Tooltip = new SeStringBuilder()
                .Append("Supports Regex Search")
                .AppendNewLine()
                .Append("Start input with '$' to search by description")
                .ToReadOnlySeString(),
        };
        _helpNode.AttachNode(this);
    }

    public required Action<ReadOnlySeString>? OnInputReceived {
        get => _textInputNode.OnInputReceived;
        set => _textInputNode.OnInputReceived = value;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        _helpNode.Size = new Vector2(Height, Height);
        _helpNode.Position = new Vector2(Width - _helpNode.Width - 5.0f, 0.0f);

        _textInputNode.Size = new Vector2(Width - _helpNode.Width - 5.0f, Height);
        _textInputNode.Position = new Vector2(0.0f, 0.0f);
    }

    public ReadOnlySeString SearchString {
        get => _textInputNode.SeString;
        set => _textInputNode.SeString = value;
    }
}