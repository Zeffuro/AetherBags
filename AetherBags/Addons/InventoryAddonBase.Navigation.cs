using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Nodes.Inventory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract partial class InventoryAddonBase
{
    private InventoryNavigation? _navigation;

    protected InventoryAddonBase()
    {
        CreateWindowNode = static () => new InventoryWindowNode();
    }

    private sealed class InventoryWindowNode : WindowNode
    {
        public InventoryWindowNode() => FocusNode = WindowHeaderFocusNode;
    }

    private void InitializeNavigation()
    {
        _navigation = new InventoryNavigation(this);
        _navigation.Update();
    }

    private void UpdateNavigation() => _navigation?.Update();

    private void InvalidateNavigation() => _navigation?.Invalidate();

    private void DisposeNavigation()
    {
        _navigation?.Dispose();
        _navigation = null;
    }

    internal sealed unsafe class InventoryNavigation : IDisposable
    {
        private const AtkEventType ScrollToComponent = (AtkEventType)81;

        private readonly InventoryAddonBase _addon;
        private readonly ComponentNode[] _header;
        private readonly List<InventoryNavigationTarget> _targets = [];
        private readonly List<ComponentNode> _bound = [];
        private readonly Dictionary<ComponentNode, (Action Start, Action Stop, Action Reset, nint Focus)> _registered = [];
        private ComponentNode? _focused;
        private bool _dirty = true;
        private bool _focusSearchOnOpen;

        public InventoryNavigation(InventoryAddonBase addon)
        {
            _addon = addon;
            _header = [addon.SearchInputNode.InputNode, addon.SearchInputNode.ButtonNode, addon.SettingsButtonNode];
            ConfigureHeader();
            addon.CategoriesNode.LayoutChanged += Invalidate;
            addon.RootNode.AddEvent(ScrollToComponent, OnScrollToComponent);
        }

        private void ConfigureHeader()
        {
            var window = _addon.WindowNode!;
            var uiModule = UIModule.Instance();
            _focusSearchOnOpen = System.Config.General.FocusSearchOnOpen
                && uiModule != null && !uiModule->IsPadModeEnabled();
            window.NavIndex = 1;
            _header[0].NavIndex = 2;
            _header[1].NavIndex = 3;
            _header[2].NavIndex = 9;
            window.NavUp = window.NavDown = window.NavLeft = _header[0].NavIndex;
            window.NavRight = _header[2].NavIndex;
            Register(window);
            for (int i = 0; i < _header.Length; i++)
            {
                var node = _header[i];
                node.NavLeft = _header[(i + 2) % _header.Length].NavIndex;
                node.NavRight = _header[(i + 1) % _header.Length].NavIndex;
                Register(node);
            }
        }

        internal void Invalidate() => _dirty = true;

        public void Update()
        {
            FocusSearchOnOpen();
            if (!_dirty) return;
            _dirty = false;
            ClearBindings();
            _targets.Clear();
            foreach (var node in _addon.CategoriesNode.Nodes)
            {
                if (node is InventoryCategoryNodeBase category && category.NodeFlags.HasFlag(NodeFlags.Visible))
                    category.AddNavigationTargets(_targets);
            }
            _targets.Sort(static (a, b) =>
            {
                int row = a.Position.Y.CompareTo(b.Position.Y);
                return row != 0 ? row : a.Position.X.CompareTo(b.Position.X);
            });
            foreach (var target in _targets) Register(target.Node);
            int focusedIndex = _targets.FindIndex(t => t.Node == _focused);
            if (_focused != null && !IsHeader(_focused) && focusedIndex < 0)
            {
                var oldFocus = _focused;
                bool ownsFocus = (AtkResNode*)oldFocus.FocusNode != null
                    && AtkStage.Instance()->AtkInputManager->FocusedNode == (AtkResNode*)oldFocus.FocusNode;
                OnBlur(oldFocus);
                if (ownsFocus) FocusWindow();
            }
            else if (focusedIndex >= 0)
                EnsureVisible(_targets[focusedIndex]);
            Bind();
        }

        private void FocusSearchOnOpen()
        {
            if (!_focusSearchOnOpen) return;
            AtkUnitBase* addon = _addon;
            if (addon == null || !addon->IsReady || !addon->IsVisible) return;
            _focusSearchOnOpen = false;
            var manager = RaptureAtkUnitManager.Instance();
            if (manager == null || manager->FocusedAddon != addon) return;
            var input = AtkStage.Instance()->AtkInputManager;
            if (input->FocusedNode != null && manager->GetAddonByNode(input->FocusedNode) != addon) return;
            addon->SetFocusNode(_header[0].FocusNode);
        }

        private void Register(ComponentNode node)
        {
            if (_registered.ContainsKey(node)) return;
            Action start = () => OnFocus(node);
            Action stop = () => OnBlur(node);
            Action reset = () => OnReset(node);
            _registered.Add(node, (start, stop, reset, (nint)(AtkResNode*)node.FocusNode));
            node.FocusNode.AddEvent(AtkEventType.FocusStart, start);
            node.FocusNode.AddEvent(AtkEventType.FocusStop, stop);
            if (node is InventoryDragDropNode item) item.NavigationReset += reset;
            if (node is LootedItemDisplayNode loot) loot.NavigationReset += reset;
        }

        private void OnFocus(ComponentNode node)
        {
            _focused = node;
            Update();
            Bind();
            int index = _targets.FindIndex(t => t.Node == node);
            if (index < 0 && !IsHeader(node))
            {
                OnBlur(node);
                FocusWindow();
                return;
            }
            if (index >= 0) EnsureVisible(_targets[index]);
            if (_focused == node && index >= 0 && node is InventoryDragDropNode item)
                item.ShowInventoryItemTooltip(item.ItemInfo.Item.Container, item.ItemInfo.Item.Slot);
        }

        private void OnBlur(ComponentNode node)
        {
            if (_focused != node) return;
            _focused = null;
            if (node is InventoryDragDropNode)
            {
                var addon = FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance()->GetAddonByNode(_addon.WindowNode!);
                if (addon != null) AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            }
        }

        private void OnReset(ComponentNode node)
        {
            OnBlur(node);
            var oldFocus = _registered.TryGetValue(node, out var registered)
                ? (AtkResNode*)registered.Focus : (AtkResNode*)node.FocusNode;
            if (oldFocus != null)
            {
                var addon = FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance()->GetAddonByNode(_addon.WindowNode!);
                if (AtkStage.Instance()->AtkInputManager->FocusedNode == oldFocus) FocusWindow();
                if (addon != null && addon->FocusNode == oldFocus) addon->FocusNode = _addon.WindowNode!.FocusNode;
                if (addon != null && addon->ComponentFocusNode == oldFocus) addon->SetComponentFocusNode(null);
            }
            ClearBindings();
            foreach (var header in _header) header.NavUp = header.NavDown = 0;
            _addon.WindowNode!.NavUp = _addon.WindowNode!.NavDown = _addon.WindowNode!.NavIndex;
            Unregister(node);
            _dirty = true;
        }

        private bool IsHeader(ComponentNode node) => node == _addon.WindowNode! || Array.IndexOf(_header, node) >= 0;

        private void FocusWindow()
        {
            var addon = FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance()->GetAddonByNode(_addon.WindowNode!);
            if (addon != null) addon->SetFocusNode(_addon.WindowNode!.FocusNode, true);
        }

        private void Unregister(ComponentNode node)
        {
            if (!_registered.Remove(node, out var callbacks)) return;
            if ((AtkResNode*)node.FocusNode != null)
            {
                node.FocusNode.RemoveEvent(AtkEventType.FocusStart, callbacks.Start);
                node.FocusNode.RemoveEvent(AtkEventType.FocusStop, callbacks.Stop);
            }
            if (node is InventoryDragDropNode item) item.NavigationReset -= callbacks.Reset;
            if (node is LootedItemDisplayNode loot) loot.NavigationReset -= callbacks.Reset;
        }

        private void ClearBindings()
        {
            foreach (var node in _bound)
            {
                if ((AtkResNode*)node == null) continue;
                node.NavIndex = node.NavLeft = node.NavRight = node.NavUp = node.NavDown = 0;
            }
            _bound.Clear();
        }

        private int BindTarget(int index)
        {
            if (index < 0) return _addon.WindowNode!.NavIndex;
            var node = _targets[index].Node;
            int existing = _bound.IndexOf(node);
            if (existing >= 0) return existing + 4;
            int id = _bound.Count + 4;
            _bound.Add(node);
            node.NavIndex = id;
            return id;
        }

        private void Bind()
        {
            ClearBindings();
            int index = _targets.FindIndex(t => t.Node == _focused);
            if (index >= 0)
            {
                var node = _targets[index].Node;
                BindTarget(index);
                node.NavLeft = BindTarget((index + _targets.Count - 1) % _targets.Count);
                node.NavRight = BindTarget((index + 1) % _targets.Count);
                node.NavUp = BindTarget(FindVertical(index, -1));
                node.NavDown = BindTarget(FindVertical(index, 1));
                _addon.WindowNode!.NavUp = _addon.WindowNode!.NavDown = node.NavIndex;
                foreach (var header in _header) header.NavUp = header.NavDown = node.NavIndex;
            }
            else
            {
                int first = _targets.Count == 0 ? _addon.WindowNode!.NavIndex : BindTarget(0);
                int last = _targets.Count == 0 ? _addon.WindowNode!.NavIndex : BindTarget(_targets.Count - 1);
                _addon.WindowNode!.NavDown = first;
                _addon.WindowNode!.NavUp = last;
                foreach (var header in _header)
                {
                    header.NavUp = last;
                    header.NavDown = first;
                }
            }
        }

        private int FindVertical(int index, int direction)
        {
            var current = _targets[index];
            Vector2 center = current.Position + current.Node.Size / 2;
            int best = -1;
            bool bestOverlaps = false;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                Vector2 delta = target.Position + target.Node.Size / 2 - center;
                if (delta.Y * direction <= 1) continue;
                bool overlaps = target.Position.X < current.Position.X + current.Node.Width
                    && target.Position.X + target.Node.Width > current.Position.X;
                float distance = MathF.Abs(delta.Y) + MathF.Abs(delta.X);
                if (best >= 0 && (bestOverlaps && !overlaps || overlaps == bestOverlaps && distance >= bestDistance)) continue;
                best = i;
                bestOverlaps = overlaps;
                bestDistance = distance;
            }
            return best;
        }

        private void OnScrollToComponent(AtkEventListener* listener, AtkEventType type, int param,
            AtkEvent* atkEvent, AtkEventData* data)
        {
            if (data == null) return;
            Update();
            var component = *(AtkComponentBase**)data;
            foreach (var target in _targets)
            {
                if ((AtkResNode*)target.Node == null || target.Node.ComponentBase != component) continue;
                EnsureVisible(target);
                return;
            }
        }

        private void EnsureVisible(InventoryNavigationTarget target)
        {
            var bar = _addon.ScrollableCategories.ScrollBarNode;
            float position = bar.ScrollPosition;
            if (target.Position.Y < position) position = target.Position.Y;
            else if (target.Position.Y + target.Node.Height > position + _addon.ScrollableCategories.Height)
                position = target.Position.Y + target.Node.Height - _addon.ScrollableCategories.Height;
            float next = Math.Clamp(position, 0, bar.ScrollMaxPosition);
            if (next == bar.ScrollPosition) return;
            bar.ScrollPosition = next;
            _addon.OnScrollValueChanged((int)bar.ScrollPosition);
        }

        public void Dispose()
        {
            _addon.CategoriesNode.LayoutChanged -= Invalidate;
            _addon.RootNode.RemoveEvent(ScrollToComponent, OnScrollToComponent);
            ClearBindings();
            foreach (var (node, callbacks) in _registered)
            {
                if ((AtkResNode*)node != null)
                {
                    if (AtkStage.Instance()->AtkInputManager->FocusedNode == (AtkResNode*)node.FocusNode)
                        AtkStage.Instance()->ClearNodeFocus(node.FocusNode);
                    node.FocusNode.RemoveEvent(AtkEventType.FocusStart, callbacks.Start);
                    node.FocusNode.RemoveEvent(AtkEventType.FocusStop, callbacks.Stop);
                }
                if (node is InventoryDragDropNode item) item.NavigationReset -= callbacks.Reset;
                if (node is LootedItemDisplayNode loot) loot.NavigationReset -= callbacks.Reset;
            }
            _registered.Clear();
            _targets.Clear();
            _focused = null;
        }
    }
}
