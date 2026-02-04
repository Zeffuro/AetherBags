using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KamiToolKit;

namespace AetherBags.Nodes.Layout;

public sealed class SharedNodePool<T> where T : NodeBase
{
    private readonly Stack<T> _pool;
    private readonly int _maxSize;
    private readonly Func<T>? _factory;
    private readonly Action<T>? _resetAction;

    public SharedNodePool(int maxSize = 128, Func<T>? factory = null, Action<T>? resetAction = null)
    {
        _maxSize = maxSize;
        _factory = factory;
        _resetAction = resetAction;
        _pool = new Stack<T>(Math.Min(maxSize, 64));
    }

    public int Count => _pool.Count;
    public int MaxSize => _maxSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? TryRent()
    {
        if (_pool.TryPop(out var node))
        {
            node.IsVisible = true;
            return node;
        }
        return null;
    }

    public T RentOrCreate()
    {
        if (_pool.TryPop(out var node))
        {
            node.IsVisible = true;
            return node;
        }

        if (_factory == null)
            throw new InvalidOperationException("No factory provided and pool is empty");

        return _factory();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReturn(T node)
    {
        if (_pool.Count >= _maxSize)
            return false;

        _resetAction?.Invoke(node);
        node.IsVisible = false;
        node.DetachNode();
        _pool.Push(node);
        return true;
    }

    public void Return(T node)
    {
        if (!TryReturn(node))
        {
            try
            {
                node.Dispose();
            }
            catch (Exception ex)
            {
                Services.Logger.Error(ex, $"[SharedNodePool] Error disposing overflow node {typeof(T).Name}");
            }
        }
    }

    public void Clear()
    {
        while (_pool.TryPop(out var node))
        {
            try
            {
                node.Dispose();
            }
            catch (Exception ex)
            {
                Services.Logger.Error(ex, $"[SharedNodePool] Error disposing pooled node {typeof(T).Name}");
            }
        }
    }

    public void Prewarm(int count)
    {
        if (_factory == null)
            return;

        count = Math.Min(count, _maxSize - _pool.Count);
        for (int i = 0; i < count; i++)
        {
            var node = _factory();
            node.IsVisible = false;
            _pool.Push(node);
        }
    }
}
