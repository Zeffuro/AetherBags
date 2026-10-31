using System;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Nodes.Inventory;

namespace AetherBags.Addons;

public abstract partial class InventoryAddonBase
{
    private readonly record struct EffectiveWindowSizing
    {
        public InventoryWindowSizingMode Mode { get; init; }
        public float FixedWidth { get; init; }
        public float FixedHeight { get; init; }
        public float MinWidth { get; init; }
        public float MaxWidth { get; init; }
        public float MinHeight { get; init; }
        public float MaxHeight { get; init; }
    }

    private EffectiveWindowSizing GetEffectiveWindowSizing()
    {
        var settings = WindowSizingSettings;
        InventoryWindowSizingDefaults.Normalize(settings, WindowSizingLimits);

        return settings.Mode switch
        {
            InventoryWindowSizingMode.Fixed => new EffectiveWindowSizing
            {
                Mode = settings.Mode,
                FixedWidth = settings.FixedWidth,
                FixedHeight = settings.FixedHeight,
                MinWidth = MinWindowWidth,
                MaxWidth = InventoryWindowSizingDefaults.MaxConfigurableWidth,
                MinHeight = MinWindowHeight,
                MaxHeight = InventoryWindowSizingDefaults.MaxConfigurableHeight,
            },
            InventoryWindowSizingMode.CustomBounds => new EffectiveWindowSizing
            {
                Mode = settings.Mode,
                MinWidth = settings.MinWidth,
                MaxWidth = settings.MaxWidth,
                MinHeight = settings.MinHeight,
                MaxHeight = settings.MaxHeight,
            },
            _ => new EffectiveWindowSizing
            {
                Mode = InventoryWindowSizingMode.Automatic,
                MinWidth = MinWindowWidth,
                MaxWidth = MaxWindowWidth,
                MinHeight = MinWindowHeight,
                MaxHeight = MaxWindowHeight,
            }
        };
    }

    protected virtual void LayoutContent()
    {
        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerH = HasFooter || HasSlotCounter ?  FooterHeight : 0;

        if (HasFooter)
        {
            FooterNode.Position = new Vector2(contentPos.X, contentPos.Y + contentSize.Y - footerH);
            FooterNode.Size = new Vector2(contentSize.X, footerH);
        }
        else if (HasSlotCounter && SlotCounterNode != null)
        {
            SlotCounterNode.Position = new Vector2(contentSize.X -80f, contentPos.Y + contentSize.Y - footerH + 4f);
        }

        float gridH = contentSize.Y - ((HasFooter || HasSlotCounter) ? FooterHeight + FooterTopSpacing : 0);
        if (gridH < 0) gridH = 0;

        ScrollableCategories.Position = contentPos;
        ScrollableCategories.Size = new Vector2(contentSize.X, gridH);

        float categoriesWidth = contentSize.X - ScrollBarWidth;
        CategoriesNode.Width = categoriesWidth;

        UpdateCategoryMaxWidths(categoriesWidth);
    }

    private void UpdateCategoryMaxWidths(float maxWidth)
    {
        foreach (var node in CategoriesNode.Nodes)
        {
            if (node is InventoryCategoryNodeBase categoryNode && categoryNode.MaxWidth != maxWidth)
            {
                categoryNode.MaxWidth = maxWidth;
                categoryNode.RecalculateSize();
            }
        }
    }

    protected virtual void AutoSizeWindow()
    {
        if (KeepLayoutWindowSize()) return;
        var sizing = GetEffectiveWindowSizing();

        if (sizing.Mode == InventoryWindowSizingMode.Fixed)
        {
            ResizeWindow(sizing.FixedWidth, sizing.FixedHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

        var nodes = CategoriesNode.Nodes;

        float maxChildWidth = 0f;
        int childCount = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryCategoryNodeBase cat)
                continue;

            childCount++;
            float w = cat.Width;
            if (w > maxChildWidth) maxChildWidth = w;
        }

        if (childCount == 0)
        {
            ResizeWindow(sizing.MinWidth, sizing.MinHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

        float footerSpace = HasFooter || HasSlotCounter ? FooterHeight + FooterTopSpacing : 0;

        float requiredWidth = maxChildWidth + ScrollBarWidth + (ContentStartPosition.X * 2);
        float finalWidth = Math.Clamp(requiredWidth, sizing.MinWidth, sizing.MaxWidth);

        if (SettingsButtonNode != null)
        {
            SettingsButtonNode.X = finalWidth - SettingsButtonOffset;
        }

        float contentWidth = finalWidth - (ContentStartPosition.X * 2);
        float categoriesWidth = contentWidth - ScrollBarWidth;

        CategoriesNode.Width = categoriesWidth;
        UpdateCategoryMaxWidths(categoriesWidth);
        CategoriesNode.RecalculateLayout();

        float requiredGridHeight = CategoriesNode.GetRequiredHeight();

        float requiredContentHeight = requiredGridHeight + footerSpace;
        float requiredWindowHeight = requiredContentHeight + ContentStartPosition.Y + ContentStartPosition.X + ContentHeightOffset;
        float finalHeight = Math.Clamp(requiredWindowHeight, sizing.MinHeight, sizing.MaxHeight);

        ResizeWindow(finalWidth, finalHeight, recalcLayout: false);

        UpdateScrollParameters();
    }

    protected void UpdateScrollParameters()
    {
        if (ScrollableCategories == null) return;

        float requiredHeight = CategoriesNode.GetRequiredHeight();
        ScrollableCategories.ContentHeight = requiredHeight;
        UpdateLayoutWarning();

        CategoryVirtualization.ViewportHeight = ScrollableCategories.Size.Y;
        CategoryVirtualization.ScrollPosition = ScrollableCategories.ScrollBarNode.ScrollPosition;
        UpdateCategoryVisibility();
    }

    private void OnScrollValueChanged(int scrollPosition)
    {
        CategoryVirtualization.ScrollPosition = scrollPosition;
    }

    private void UpdateCategoryVisibility()
    {
        var nodes = CategoriesNode.Nodes;
        CategoryVirtualization.SetItemCount(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is InventoryCategoryNodeBase cat)
            {
                CategoryVirtualization.SetItemLayout(i, cat.Y, cat.Height);
            }
        }

        CategoryVirtualization.UpdateVisibility();
    }

    protected void ResizeWindow(float width, float height, bool recalcLayout)
    {
        SetWindowSize(width, height);

        if (BackgroundDropTarget != null)
        {
            BackgroundDropTarget.Size = ContentSize;
        }

        UpdateHeaderLayout();
        LayoutContent();

        if (recalcLayout)
            CategoriesNode.RecalculateLayout();

        UpdateScrollParameters();
    }

    protected virtual void UpdateHeaderLayout()
    {
        var header = CalculateHeaderLayout();

        if (SearchInputNode != null)
        {
            SearchInputNode.Position = header.SearchPosition;
            SearchInputNode.Size = header.SearchSize;
        }

        if (SettingsButtonNode != null)
        {
            SettingsButtonNode.Position = new Vector2(header.HeaderWidth - SettingsButtonOffset, header.HeaderY);
        }
    }

    protected void ResizeWindow(float width, float height)
        => ResizeWindow(width, height, recalcLayout: true);

}
