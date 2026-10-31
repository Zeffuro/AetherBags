using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Components.Search;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Addons;

public abstract class SearchPickerAddon<T, TNode> : AbstractSearchAddon<T, TNode>
    where TNode : ListItemNode<T>, IListItemNode, new()
{
    private StringDropDownNode? _sortModeNode;
    private TextButtonNode? _sortDirectionNode;
    private bool _reversed;

    private object? _owner;
    private bool _isDisposing;

    public Action<T>? OnAddClicked { get; set; }

    public unsafe void OpenFor(object owner, Action<T> onAdd)
    {
        if (Plugin.IsUnloading || _isDisposing || (InternalAddon is not null && !IsOpen)) return;

        if (!ReferenceEquals(_owner, owner))
        {
            ResultsListNode?.ClearSelection();
            ResultsListNode?.ResetScroll();
        }
        _owner = owner;
        OnAddClicked = selection =>
        {
            if (!Plugin.IsUnloading && !_isDisposing && ReferenceEquals(_owner, owner)) onAdd(selection);
        };
        ConfirmedSelections = selections =>
        {
            if (!ReferenceEquals(_owner, owner)) return;
            foreach (var selection in selections) OnAddClicked?.Invoke(selection);
        };
        BindRowCallbacks();
        Open();
    }

    public void CloseFor(object owner)
    {
        if (!ReferenceEquals(_owner, owner)) return;
        _owner = null;
        OnAddClicked = null;
        ConfirmedSelections = null;
        BindRowCallbacks();
        Close();
    }

    public override async ValueTask DisposeAsync()
    {
        _isDisposing = true;
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected abstract int Compare(T left, T right);
    protected virtual int CompareId(T left, T right) => Compare(left, right);
    protected abstract Func<T, bool> CreateSearchMatch(string search);
    protected virtual void BindRowCallbacks() { }

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        base.OnSetup(addon, atkValueSpan);

        if (LayoutContainer is null || ResultsListNode is null) return;
        LayoutContainer.Position += new Vector2(0, 34);
        LayoutContainer.Height -= 34;
        ResultsListNode.Height -= 34;
        ResultsListNode.ItemSpacing = 6;
        LayoutContainer.RecalculateLayout();

        _sortModeNode = new StringDropDownNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(ContentSize.X - 130, 26),
            Options = ["Alphabetical", "ID"],
            SelectedOption = "Alphabetical",
            OnOptionSelected = _ => RefreshResults(),
        };
        _sortModeNode.AttachNode(this);

        _reversed = false;
        _sortDirectionNode = new TextButtonNode
        {
            Position = ContentStartPosition + new Vector2(ContentSize.X - 120, 0),
            Size = new Vector2(120, 26),
            String = "Ascending",
            OnClick = () =>
            {
                _reversed = !_reversed;
                _sortDirectionNode!.String = _reversed ? "Descending" : "Ascending";
                RefreshResults();
            },
        };
        _sortDirectionNode.AttachNode(this);
        RefreshResults();
    }

    protected override void OnSearchInputReceived(ReadOnlySeString searchString)
    {
        RefreshResults();
        base.OnSearchInputReceived(searchString);
    }

    private void RefreshResults()
    {
        if (ResultsListNode is null) return;
        var matches = CreateSearchMatch(SearchInputNode?.String.ExtractText() ?? string.Empty);
        var options = OptionsList.Where(matches).ToList();
        options.Sort((left, right) => (_sortModeNode?.SelectedOption == "ID" ? CompareId(left, right) : Compare(left, right)) * (_reversed ? -1 : 1));
        ResultsListNode.OptionsList = options;
        BindRowCallbacks();
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        _owner = null;
        OnAddClicked = null;
        ConfirmedSelections = null;
        _sortModeNode = null;
        _sortDirectionNode = null;
        base.OnFinalize(addon);
    }
}
