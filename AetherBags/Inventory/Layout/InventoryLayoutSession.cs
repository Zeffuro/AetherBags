using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Items;

namespace AetherBags.Inventory.Layout;

public sealed class InventoryLayoutSession
{
    private readonly List<HeldBlock> _blocks = [];
    private readonly Dictionary<uint, HeldCategory> _categories = [];
    private uint _generation;
    private bool _holding;
    private bool _holdingItemSlots;

    public IReadOnlyList<InventoryLayoutBlock> Update(
        IReadOnlyList<CategorizedInventory> current,
        int maxColumns,
        bool hold,
        bool holdItemSlots)
    {
        maxColumns = Math.Max(1, maxColumns);
        if (!hold)
        {
            Reset();
            var live = new List<InventoryLayoutBlock>(current.Count);
            foreach (var inventory in current)
                live.Add(new InventoryLayoutBlock(inventory.Key,
                    new CategorizedInventory(inventory.Key, CopyCategory(inventory.Category), [..inventory.Items]),
                    Math.Max(1, Math.Min(inventory.Items.Count, maxColumns)), inventory.Items.Count, null));
            return live;
        }

        if (!_holding || _holdingItemSlots != holdItemSlots)
            Reset();
        _holding = true;
        _holdingItemSlots = holdItemSlots;

        var inputs = new Dictionary<uint, CategorizedInventory>(current.Count);
        foreach (var inventory in current)
        {
            inputs.Add(inventory.Key, inventory);
            if (!_categories.TryGetValue(inventory.Key, out var category))
            {
                category = new HeldCategory(inventory.Key, CategorySnapshot.Capture(inventory.Category));
                _categories.Add(inventory.Key, category);
                var columns = Math.Max(1, Math.Min(inventory.Items.Count, maxColumns));
                AddBlock(category, columns, RoundCapacity(inventory.Items.Count, columns), true);
            }
            category.Metadata = CategorySnapshot.Capture(inventory.Category);
        }

        var items = new Dictionary<HeldBlock, List<ItemInfo>>(_blocks.Count);
        var slots = holdItemSlots ? new Dictionary<HeldBlock, Dictionary<ulong, int>>(_blocks.Count) : null;
        foreach (var category in _categories.Values)
        {
            var present = inputs.TryGetValue(category.Key, out var inventory) ? inventory.Items : null;
            if (holdItemSlots)
                UpdateItemSlots(category, present, maxColumns, items, slots!);
            else
                UpdateCategorySpace(category, present, maxColumns, items);
        }

        var result = new List<InventoryLayoutBlock>(_blocks.Count);
        foreach (var block in _blocks)
        {
            items.TryGetValue(block, out var blockItems);
            Dictionary<ulong, int>? blockSlots = null;
            slots?.TryGetValue(block, out blockSlots);
            result.Add(new InventoryLayoutBlock(block.Key,
                new CategorizedInventory(block.Category.Key, block.Category.Metadata.ToInfo(), blockItems ?? []),
                block.Columns, block.Capacity, holdItemSlots ? blockSlots ?? [] : null));
        }
        return result;
    }

    public void Reset()
    {
        _blocks.Clear();
        _categories.Clear();
        _generation = 0;
        _holding = false;
        _holdingItemSlots = false;
    }

    private void UpdateCategorySpace(
        HeldCategory category,
        List<ItemInfo>? current,
        int maxColumns,
        Dictionary<HeldBlock, List<ItemInfo>> items)
    {
        if (current is null || current.Count == 0)
            return;

        var offset = 0;
        foreach (var block in category.Blocks)
        {
            var count = Math.Min(block.Capacity, current.Count - offset);
            if (count > 0)
            {
                items.Add(block, current.GetRange(offset, count));
                offset += count;
            }
            if (offset == current.Count)
                return;
        }

        var remaining = current.Count - offset;
        var continuation = AddBlock(category, maxColumns, RoundCapacity(remaining, maxColumns), false);
        items.Add(continuation, current.GetRange(offset, remaining));
    }

    private void UpdateItemSlots(
        HeldCategory category,
        List<ItemInfo>? current,
        int maxColumns,
        Dictionary<HeldBlock, List<ItemInfo>> items,
        Dictionary<HeldBlock, Dictionary<ulong, int>> slots)
    {
        var next = new Dictionary<ItemIdentity, ItemPlacement>(current?.Count ?? 0);
        if (current is not null)
        {
            foreach (var item in current)
            {
                var identity = new ItemIdentity(item.Key, item.Item.ItemId, (int)item.Item.Flags);
                if (!category.Active.TryGetValue(identity, out var placement))
                {
                    while (category.NextAvailableBlock < category.Blocks.Count)
                    {
                        var block = category.Blocks[category.NextAvailableBlock];
                        if (block.NextUnusedCell < block.Capacity)
                            break;
                        category.NextAvailableBlock++;
                    }
                    var destination = category.NextAvailableBlock < category.Blocks.Count
                        ? category.Blocks[category.NextAvailableBlock]
                        : AddBlock(category, maxColumns, maxColumns, false);
                    placement = new ItemPlacement(destination, destination.NextUnusedCell++);
                }
                next.Add(identity, placement);
                if (!items.TryGetValue(placement.Block, out var blockItems))
                    items.Add(placement.Block, blockItems = []);
                blockItems.Add(item);
                if (!slots.TryGetValue(placement.Block, out var blockSlots))
                    slots.Add(placement.Block, blockSlots = []);
                blockSlots.Add(item.Key, placement.Cell);
            }
        }
        category.Active = next;
    }

    private HeldBlock AddBlock(HeldCategory category, int columns, int capacity, bool first)
    {
        var key = first ? category.Key : ((ulong)checked(++_generation) << 32) | category.Key;
        var block = new HeldBlock(key, category, columns, capacity);
        category.Blocks.Add(block);
        _blocks.Add(block);
        return block;
    }

    private static int RoundCapacity(int count, int columns)
        => checked((int)(((long)count + columns - 1) / columns * columns));

    private static CategoryInfo CopyCategory(CategoryInfo category)
        => CategorySnapshot.Capture(category).ToInfo();

    private readonly record struct ItemIdentity(ulong Key, uint ItemId, int Flags);
    private readonly record struct ItemPlacement(HeldBlock Block, int Cell);

    private sealed class HeldCategory(uint key, CategorySnapshot metadata)
    {
        public uint Key { get; } = key;
        public CategorySnapshot Metadata { get; set; } = metadata;
        public List<HeldBlock> Blocks { get; } = [];
        public Dictionary<ItemIdentity, ItemPlacement> Active { get; set; } = [];
        public int NextAvailableBlock { get; set; }
    }

    private sealed class HeldBlock(ulong key, HeldCategory category, int columns, int capacity)
    {
        public ulong Key { get; } = key;
        public HeldCategory Category { get; } = category;
        public int Columns { get; } = columns;
        public int Capacity { get; } = capacity;
        public int NextUnusedCell { get; set; }
    }

    private readonly record struct CategorySnapshot(string Name, Vector4 Color, string Description, bool IsPinned, int Order)
    {
        public static CategorySnapshot Capture(CategoryInfo category)
            => new(category.Name, category.Color, category.Description, category.IsPinned, category.Order);

        public CategoryInfo ToInfo()
            => new() { Name = Name, Color = Color, Description = Description, IsPinned = IsPinned, Order = Order };
    }
}
