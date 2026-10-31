using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

public abstract partial class DeferrableLayoutListNode : ResNode, ILayoutListNode
{
    public int NavIndex { get; set; }

    protected readonly List<NodeBase> NodeList = [];
    private bool _suppressRecalculateLayout;
    private int _deferRecalcDepth;
    private bool _pendingRecalc;

    private readonly Dictionary<Type, Stack<NodeBase>> _nodePoolByType = new();
    private const int MaxPoolSizePerType = 64;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TU? TryRentFromPool<TU>(SharedNodePool<TU>? externalPool) where TU : NodeBase
    {
        if (externalPool != null)
        {
            return externalPool.TryRent();
        }

        if (_nodePoolByType.TryGetValue(typeof(TU), out var pool) && pool.Count > 0)
        {
            var node = (TU)pool.Pop();
            node.IsVisible = true;
            return node;
        }
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReturnToPool<TU>(TU node, SharedNodePool<TU>? externalPool, Action<TU>? resetAction) where TU : NodeBase
    {
        if (externalPool != null)
        {
            resetAction?.Invoke(node);
            return externalPool.TryReturn(node);
        }

        var type = typeof(TU);
        if (!_nodePoolByType.TryGetValue(type, out var pool))
        {
            pool = new Stack<NodeBase>(16);
            _nodePoolByType[type] = pool;
        }

        if (pool.Count >= MaxPoolSizePerType)
            return false;

        resetAction?.Invoke(node);
        node.IsVisible = false;
        node.DetachNode();
        pool.Push(node);
        return true;
    }

    private void DisposePool()
    {
        foreach (var pool in _nodePoolByType.Values)
        {
            while (pool.Count > 0)
            {
                var node = pool.Pop();
                SafeDisposeNode(node);
            }
        }
        _nodePoolByType.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void SafeDisposeNode(NodeBase node)
    {
        try
        {
            node.Dispose();
        }
        catch (Exception ex)
        {
            IPluginLog.Get().Error(ex, $"[SafeDisposeNode] Error disposing {node.GetType().Name}");
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
                AddNodeFlags(NodeFlags.Clip);
            else
                RemoveNodeFlags(NodeFlags.Clip);
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
            if (NodeList[i] is ILayoutListNode subNode)
                subNode.RecalculateLayout();
        }
    }

    protected virtual void AdjustNode(NodeBase node) { }

    protected abstract void InternalRecalculateLayout();

    public ICollection<NodeBase> InitialNodes
    {
        set => AddNode(value);
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

    public void RemoveNode(params NodeBase[] items) => RemoveNode((IEnumerable<NodeBase>)items);

    public void RemoveNode(IEnumerable<NodeBase> items)
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
        SafeDisposeNode(node);

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
                SafeDisposeNode(node);
            }
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }

        DisposePool();

        RecalculateLayout();
    }

    public void ClearListOnly()
    {
        _suppressRecalculateLayout = true;
        try
        {
            NodeList.Clear();
        }
        finally
        {
            _suppressRecalculateLayout = false;
        }

        RecalculateLayout();
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

    protected override void Dispose(bool isNativeDestructor)
    {
        if (IsDisposed) return;

        // Pooled nodes are detached, so the normal child traversal cannot release them.
        DisposePool();
        NodeList.Clear();
        base.Dispose(isNativeDestructor);
    }
}
