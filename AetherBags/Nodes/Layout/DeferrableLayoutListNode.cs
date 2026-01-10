using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

public abstract class DeferrableLayoutListNode : SimpleComponentNode
{
    protected readonly List<NodeBase> NodeList = [];
    private bool _suppressRecalculateLayout;
    private int _deferRecalcDepth;
    private bool _pendingRecalc;

    /// <summary>
    /// Hide and detach a node from the UI tree without disposing it.
    /// Disposal happens later when KamiToolKit cleans up detached nodes.
    /// </summary>
    protected static void SafeDetachNode(NodeBase node)
    {
        try
        {
            node.IsVisible = false;
            node.DetachNode();
        }
        catch (Exception ex)
        {
            Services.Logger.Error(ex, $"[SafeDetachNode] Error detaching {node.GetType().Name}");
        }
    }

    public IEnumerable<T> GetNodes<T>() where T : NodeBase
    {
        for (int i = 0; i < NodeList.Count; i++)
        {
            if (NodeList[i] is T t)
                yield return t;
        }
    }

    public IReadOnlyList<NodeBase> Nodes => NodeList;

    public bool ClipListContents
    {
        get => NodeFlags.HasFlag(NodeFlags.Clip);
        set
        {
            if (value)
                AddFlags(NodeFlags.Clip);
            else
                RemoveFlags(NodeFlags.Clip);
        }
    }

    public float ItemSpacing { get; set; }

    public float FirstItemSpacing { get; set; }

    public void RecalculateLayout()
    {
        if (_suppressRecalculateLayout) return;

        if (_deferRecalcDepth > 0)
        {
            _pendingRecalc = true;
            return;
        }

        InternalRecalculateLayout();

        for (int i = 0; i < NodeList.Count; i++)
        {
            if (NodeList[i] is DeferrableLayoutListNode subNode)
                subNode.RecalculateLayout();
        }
    }

    [Obsolete]
    protected virtual void AdjustNode(NodeBase node) { }

    protected abstract void InternalRecalculateLayout();

    public ICollection<NodeBase> InitialNodes
    {
        init => AddNode(value);
    }

    public void AddNode(IEnumerable<NodeBase> nodes)
    {
        _suppressRecalculateLayout = true;
        try
        {
            foreach (var node in nodes)
            {
                AddNode(node);
            }
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }
        RecalculateLayout();
    }

    public virtual void AddNode(NodeBase? node)
    {
        if (node is null) return;

        NodeList.Add(node);

        node.AttachNode(this);

        RecalculateLayout();
    }

    public void RemoveNode(params NodeBase[] items)
    {
        _suppressRecalculateLayout = true;
        try
        {
            foreach (var node in items)
            {
                RemoveNode(node);
            }
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }
        RecalculateLayout();
    }

    public virtual void RemoveNode(NodeBase node)
    {
        if (!NodeList.Contains(node)) return;

        NodeList.Remove(node);
        SafeDetachNode(node);

        RecalculateLayout();
    }

    public void AddDummy(float size = 0.0f)
    {
        var dummyNode = new ResNode
        {
            Size = new Vector2(size, size),
        };

        AddNode(dummyNode);
    }

    public virtual void Clear()
    {
        _suppressRecalculateLayout = true;
        try
        {
            for (int i = NodeList.Count - 1; i >= 0; i--)
            {
                var node = NodeList[i];
                NodeList.RemoveAt(i);
                SafeDetachNode(node);
            }
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }
        RecalculateLayout();
    }

    public delegate TU CreateNewNode<in T, out TU>(T data) where TU : NodeBase;

    public delegate T GetDataFromNode<out T, in TU>(TU node) where TU : NodeBase;

    private List<NodeBase>? _existingScratch;
    private List<NodeBase>? _desiredScratch;
    private List<NodeBase>? _toRemoveScratch;
    private HashSet<object>? _dataKeysScratch;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<NodeBase> RentExistingList(int capacity)
    {
        var list = _existingScratch ?? new List<NodeBase>(capacity);
        list.Clear();
        if (list.Capacity < capacity) list.Capacity = capacity;
        _existingScratch = null;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnExistingList(List<NodeBase> list)
    {
        list.Clear();
        _existingScratch = list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<NodeBase> RentDesiredList(int capacity)
    {
        var list = _desiredScratch ?? new List<NodeBase>(capacity);
        list.Clear();
        if (list.Capacity < capacity) list.Capacity = capacity;
        _desiredScratch = null;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnDesiredList(List<NodeBase> list)
    {
        list.Clear();
        _desiredScratch = list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<NodeBase> RentRemoveList(int capacity)
    {
        var list = _toRemoveScratch ?? new List<NodeBase>(capacity);
        list.Clear();
        if (list.Capacity < capacity) list.Capacity = capacity;
        _toRemoveScratch = null;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnRemoveList(List<NodeBase> list)
    {
        list.Clear();
        _toRemoveScratch = list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private HashSet<object> RentKeySet(int capacity)
    {
        var set = _dataKeysScratch ?? new HashSet<object>(capacity);
        set.Clear();
        _dataKeysScratch = null;
        return set;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnKeySet(HashSet<object> set)
    {
        set.Clear();
        _dataKeysScratch = set;
    }


    public bool SyncWithListDataByKey<T, TU, TKey>(
        IReadOnlyList<T> dataList,
        Func<T, TKey> getKeyFromData,
        Func<TU, TKey> getKeyFromNode,
        Action<TU, T> updateNode,
        CreateNewNode<T, TU> createNodeMethod,
        IEqualityComparer<TKey>? keyComparer = null) where TU : NodeBase where TKey : notnull
    {
        keyComparer ??= EqualityComparer<TKey>.Default;

        int dataCount = dataList.Count;

        var desiredKeys = RentKeySet(dataCount);
        for (int i = 0; i < dataCount; i++)
        {
            desiredKeys.Add(getKeyFromData(dataList[i])!);
        }

        var existing = RentExistingList(NodeList.Count);
        var toRemove = RentRemoveList(16);

        for (int i = 0; i < NodeList.Count; i++)
        {
            if (NodeList[i] is TU tu)
            {
                var key = getKeyFromNode(tu);
                if (desiredKeys.Contains(key))
                {
                    existing.Add(tu);
                }
                else
                {
                    toRemove.Add(tu);
                }
            }
        }

        bool structureChanged = toRemove.Count > 0;

        if (toRemove.Count > 0)
        {
            _suppressRecalculateLayout = true;
            try
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    var node = toRemove[i];
                    NodeList.Remove(node);
                    SafeDetachNode(node);
                }
            }
            finally
            {
                _suppressRecalculateLayout = false;
            }
        }

        Dictionary<TKey, TU>? byKey = null;
        if (existing.Count > 0)
        {
            byKey = new Dictionary<TKey, TU>(existing.Count, keyComparer);
            for (int i = 0; i < existing.Count; i++)
            {
                var tu = (TU)existing[i];
                var key = getKeyFromNode(tu);
                byKey.TryAdd(key, tu);
            }
        }

        var desired = RentDesiredList(dataCount);

        _suppressRecalculateLayout = true;
        try
        {
            for (int i = 0; i < dataCount; i++)
            {
                var data = dataList[i];
                var key = getKeyFromData(data);

                if (byKey != null && byKey.TryGetValue(key, out var existingNode))
                {
                    updateNode(existingNode, data);
                    desired.Add(existingNode);
                    byKey.Remove(key);
                }
                else
                {
                    var newNode = createNodeMethod(data);
                    NodeList.Add(newNode);
                    newNode.AttachNode(this);
                    updateNode(newNode, data);
                    desired.Add(newNode);
                    structureChanged = true;
                }
            }
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }

        bool orderChanged = false;
        if (!structureChanged && desired.Count > 0)
        {
            int tuIndex = 0;
            for (int i = 0; i < NodeList.Count && tuIndex < desired.Count; i++)
            {
                if (NodeList[i] is TU)
                {
                    if (!ReferenceEquals(NodeList[i], desired[tuIndex]))
                    {
                        orderChanged = true;
                        break;
                    }
                    tuIndex++;
                }
            }
            if (tuIndex != desired.Count)
                orderChanged = true;
        }

        if (structureChanged || orderChanged)
        {
            int insertIndex = -1;
            for (int i = 0; i < NodeList.Count; i++)
            {
                if (NodeList[i] is TU)
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
                insertIndex = NodeList.Count;

            for (int i = NodeList.Count - 1; i >= 0; i--)
            {
                if (NodeList[i] is TU)
                    NodeList.RemoveAt(i);
            }

            if (insertIndex > NodeList.Count)
                insertIndex = NodeList.Count;

            NodeList.InsertRange(insertIndex, desired);
        }

        ReturnKeySet(desiredKeys);
        ReturnExistingList(existing);
        ReturnRemoveList(toRemove);
        ReturnDesiredList(desired);

        if (structureChanged || orderChanged)
        {
            RecalculateLayout();
        }

        return structureChanged || orderChanged;
    }

    public bool SyncWithListData<T, TU>(
        IEnumerable<T> dataList,
        GetDataFromNode<T?, TU> getDataFromNode,
        CreateNewNode<T, TU> createNodeMethod) where TU : NodeBase
    {
        _suppressRecalculateLayout = true;
        var anythingChanged = false;
        try
        {
            var existing = RentExistingList(NodeList.Count);
            for (int i = 0; i < NodeList.Count; i++)
            {
                if (NodeList[i] is TU tu)
                    existing.Add(tu);
            }

            var dataSet = new HashSet<T>(EqualityComparer<T>.Default);
            foreach (var d in dataList)
                dataSet.Add(d);

            var represented = new HashSet<T>(EqualityComparer<T>.Default);

            for (int i = 0; i < existing.Count; i++)
            {
                var tu = (TU)existing[i];
                var nodeData = getDataFromNode(tu);

                if (nodeData is null || !dataSet.Contains(nodeData))
                {
                    NodeList.Remove(tu);
                    SafeDetachNode(tu);
                    anythingChanged = true;
                    continue;
                }

                represented.Add(nodeData);
            }

            foreach (var data in dataSet)
            {
                if (represented.Contains(data))
                    continue;

                var newNode = createNodeMethod(data);
                NodeList.Add(newNode);
                newNode.AttachNode(this);
                anythingChanged = true;
            }

            ReturnExistingList(existing);
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }

        if (anythingChanged)
            RecalculateLayout();

        return anythingChanged;
    }

    public void ReorderNodes(Comparison<NodeBase> comparison)
    {
        NodeList.Sort(comparison);
        RecalculateLayout();
    }

    public IDisposable DeferRecalculateLayout()
    {
        _deferRecalcDepth++;
        return new RecalcDeferToken(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EndDefer()
    {
        _deferRecalcDepth--;
        if (_deferRecalcDepth == 0 && _pendingRecalc)
        {
            _pendingRecalc = false;
            RecalculateLayout();
        }
    }

    private readonly struct RecalcDeferToken(DeferrableLayoutListNode owner) : IDisposable
    {
        public void Dispose() => owner.EndDefer();
    }
}
