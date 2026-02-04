using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AetherBags.Nodes.Layout;

public sealed class VirtualizationState
{
    private float _scrollPosition;
    private float _viewportHeight;
    private float _bufferSize = 100f;

    private readonly List<VisibilityInfo> _itemVisibility = new(capacity: 64);

    public float ScrollPosition
    {
        get => _scrollPosition;
        set
        {
            if (MathF.Abs(_scrollPosition - value) < 0.5f) return;
            _scrollPosition = value;
            UpdateVisibility();
        }
    }

    public float ViewportHeight
    {
        get => _viewportHeight;
        set
        {
            if (MathF.Abs(_viewportHeight - value) < 0.5f) return;
            _viewportHeight = value;
            UpdateVisibility();
        }
    }

    public float BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = value;
    }

    public event Action? OnVisibilityChanged;

    public void SetItemLayout(int index, float y, float height)
    {
        while (_itemVisibility.Count <= index)
        {
            _itemVisibility.Add(new VisibilityInfo());
        }

        var info = _itemVisibility[index];
        info.Y = y;
        info.Height = height;
        _itemVisibility[index] = info;
    }

    public void ClearLayout()
    {
        _itemVisibility.Clear();
    }

    public void SetItemCount(int count)
    {
        while (_itemVisibility.Count < count)
        {
            _itemVisibility.Add(new VisibilityInfo());
        }
        if (_itemVisibility.Count > count)
        {
            _itemVisibility.RemoveRange(count, _itemVisibility.Count - count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsVisible(int index)
    {
        if (index < 0 || index >= _itemVisibility.Count)
            return false;

        return _itemVisibility[index].IsVisible;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInVisibleRange(float y, float height)
    {
        float visibleTop = _scrollPosition - _bufferSize;
        float visibleBottom = _scrollPosition + _viewportHeight + _bufferSize;

        float itemTop = y;
        float itemBottom = y + height;

        return itemBottom >= visibleTop && itemTop <= visibleBottom;
    }

    public void UpdateVisibility()
    {
        bool anyChanged = false;
        float visibleTop = _scrollPosition - _bufferSize;
        float visibleBottom = _scrollPosition + _viewportHeight + _bufferSize;

        for (int i = 0; i < _itemVisibility.Count; i++)
        {
            var info = _itemVisibility[i];
            float itemTop = info.Y;
            float itemBottom = info.Y + info.Height;

            bool wasVisible = info.IsVisible;
            bool isVisible = itemBottom >= visibleTop && itemTop <= visibleBottom;

            if (wasVisible != isVisible)
            {
                info.IsVisible = isVisible;
                _itemVisibility[i] = info;
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            OnVisibilityChanged?.Invoke();
        }
    }

    public void GetVisibleRange(out int firstVisible, out int lastVisible)
    {
        firstVisible = -1;
        lastVisible = -1;

        for (int i = 0; i < _itemVisibility.Count; i++)
        {
            if (_itemVisibility[i].IsVisible)
            {
                if (firstVisible < 0) firstVisible = i;
                lastVisible = i;
            }
        }
    }

    private struct VisibilityInfo
    {
        public float Y;
        public float Height;
        public bool IsVisible;
    }
}
