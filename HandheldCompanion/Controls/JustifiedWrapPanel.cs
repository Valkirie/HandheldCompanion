using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Controls
{
    public sealed class JustifiedWrapPanel : Panel
    {
        public static readonly DependencyProperty TargetRowHeightProperty = DependencyProperty.Register(
            nameof(TargetRowHeight),
            typeof(double),
            typeof(JustifiedWrapPanel),
            new FrameworkPropertyMetadata(250.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
            nameof(HorizontalSpacing),
            typeof(double),
            typeof(JustifiedWrapPanel),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
            nameof(VerticalSpacing),
            typeof(double),
            typeof(JustifiedWrapPanel),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty ItemAspectRatioProperty = DependencyProperty.Register(
            nameof(ItemAspectRatio),
            typeof(double),
            typeof(JustifiedWrapPanel),
            new FrameworkPropertyMetadata(200.0 / 360.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        private readonly List<RowLayout> rows = new();

        public double TargetRowHeight
        {
            get => (double)GetValue(TargetRowHeightProperty);
            set => SetValue(TargetRowHeightProperty, value);
        }

        public double HorizontalSpacing
        {
            get => (double)GetValue(HorizontalSpacingProperty);
            set => SetValue(HorizontalSpacingProperty, value);
        }

        public double VerticalSpacing
        {
            get => (double)GetValue(VerticalSpacingProperty);
            set => SetValue(VerticalSpacingProperty, value);
        }

        public double ItemAspectRatio
        {
            get => (double)GetValue(ItemAspectRatioProperty);
            set => SetValue(ItemAspectRatioProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            LayoutInfo layout = BuildLayout(availableSize);

            foreach (RowLayout row in rows)
            {
                foreach (ItemLayout item in row.Items)
                    item.Child.Measure(new Size(item.Width, item.Height));
            }

            return new Size(layout.DesiredWidth, layout.DesiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutInfo layout = BuildLayout(finalSize);
            double y = 0.0;

            foreach (RowLayout row in rows)
            {
                double x = 0.0;

                foreach (ItemLayout item in row.Items)
                {
                    item.Child.Arrange(new Rect(x, y, item.Width, item.Height));
                    x += item.Width + HorizontalSpacing;
                }

                y += row.Height + VerticalSpacing;
            }

            if (rows.Count > 0)
                y -= VerticalSpacing;

            return new Size(layout.DesiredWidth, Math.Max(layout.DesiredHeight, y));
        }

        private LayoutInfo BuildLayout(Size availableSize)
        {
            rows.Clear();

            List<UIElement> visibleChildren = new();
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Visible)
                    visibleChildren.Add(child);
            }

            if (visibleChildren.Count == 0)
                return new LayoutInfo(0.0, 0.0);

            bool hasFiniteWidth = !double.IsInfinity(availableSize.Width) && availableSize.Width > 0.0;
            double targetHeight = Math.Max(1.0, TargetRowHeight);
            double aspectRatio = Math.Max(0.01, ItemAspectRatio);
            double targetWidth = targetHeight * aspectRatio;
            List<PendingItemLayout> pendingItems = new();
            double pendingItemsWidth = 0.0;

            foreach (UIElement child in visibleChildren)
            {
                int itemSpan = Math.Max(1, GetItemSpan(child));
                double width = (targetWidth * itemSpan) + (HorizontalSpacing * (itemSpan - 1));

                pendingItems.Add(new PendingItemLayout(child, width));
                pendingItemsWidth += width;

                double currentRowWidth = pendingItemsWidth + (Math.Max(0, pendingItems.Count - 1) * HorizontalSpacing);
                if (hasFiniteWidth && currentRowWidth >= availableSize.Width)
                {
                    rows.Add(CreateRowLayout(pendingItems, availableSize.Width, targetHeight, justify: true));
                    pendingItems = new List<PendingItemLayout>();
                    pendingItemsWidth = 0.0;
                }
            }

            if (pendingItems.Count > 0)
                rows.Add(CreateRowLayout(pendingItems, availableSize.Width, targetHeight, justify: false));

            double desiredWidth = hasFiniteWidth ? availableSize.Width : GetDesiredWidth();
            double desiredHeight = 0.0;
            for (int i = 0; i < rows.Count; i++)
            {
                desiredHeight += rows[i].Height;
                if (i < rows.Count - 1)
                    desiredHeight += VerticalSpacing;
            }

            return new LayoutInfo(desiredWidth, desiredHeight);
        }

        private RowLayout CreateRowLayout(IReadOnlyList<PendingItemLayout> pendingItems, double availableWidth, double targetHeight, bool justify)
        {
            double totalItemWidth = 0.0;
            foreach (PendingItemLayout item in pendingItems)
                totalItemWidth += item.Width;

            int gapCount = Math.Max(0, pendingItems.Count - 1);
            double scale = 1.0;

            if (!double.IsInfinity(availableWidth) && availableWidth > 0.0)
            {
                double availableItemWidth = Math.Max(1.0, availableWidth - (gapCount * HorizontalSpacing));
                if (justify || totalItemWidth > availableItemWidth)
                    scale = Math.Max(0.01, availableItemWidth / totalItemWidth);
            }

            if (!justify && rows.Count > 0)
            {
                double previousRowScale = rows[^1].Height / Math.Max(1.0, targetHeight);
                scale = Math.Min(scale, previousRowScale);
            }

            double rowHeight = Math.Max(1.0, targetHeight * scale);
            RowLayout row = new(rowHeight);

            foreach (PendingItemLayout item in pendingItems)
                row.Items.Add(new ItemLayout(item.Child, Math.Max(1.0, item.Width * scale), rowHeight));

            return row;
        }

        private int GetItemSpan(UIElement child)
        {
            if (child is FrameworkElement frameworkElement && frameworkElement.Tag is not null)
            {
                if (frameworkElement.Tag is int tagInt && tagInt > 0)
                    return tagInt;

                if (frameworkElement.Tag is string tagString)
                {
                    if (int.TryParse(tagString, NumberStyles.Integer, CultureInfo.InvariantCulture, out int invariantSpan) && invariantSpan > 0)
                        return invariantSpan;

                    if (int.TryParse(tagString, NumberStyles.Integer, CultureInfo.CurrentCulture, out int currentSpan) && currentSpan > 0)
                        return currentSpan;
                }

                if (frameworkElement.Tag is double tagDouble && tagDouble >= 1.0)
                    return Math.Max(1, (int)Math.Round(tagDouble));
            }

            if (child is FrameworkElement dataElement && TryGetBooleanProperty(dataElement.DataContext, "IsLiked", out bool isLiked) && isLiked)
                return 2;

            return 1;
        }

        private static bool TryGetBooleanProperty(object? instance, string propertyName, out bool value)
        {
            value = false;

            if (instance is null)
                return false;

            PropertyInfo? propertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (propertyInfo?.PropertyType != typeof(bool))
                return false;

            object? propertyValue = propertyInfo.GetValue(instance);
            if (propertyValue is not bool booleanValue)
                return false;

            value = booleanValue;
            return true;
        }

        private double GetDesiredWidth()
        {
            double desiredWidth = 0.0;

            foreach (RowLayout row in rows)
            {
                double rowWidth = 0.0;
                for (int i = 0; i < row.Items.Count; i++)
                {
                    rowWidth += row.Items[i].Width;
                    if (i < row.Items.Count - 1)
                        rowWidth += HorizontalSpacing;
                }

                desiredWidth = Math.Max(desiredWidth, rowWidth);
            }

            return desiredWidth;
        }

        private sealed class LayoutInfo
        {
            public LayoutInfo(double desiredWidth, double desiredHeight)
            {
                DesiredWidth = desiredWidth;
                DesiredHeight = desiredHeight;
            }

            public double DesiredWidth { get; }

            public double DesiredHeight { get; }
        }

        private sealed class RowLayout
        {
            public RowLayout(double height)
            {
                Height = height;
                Items = new List<ItemLayout>();
            }

            public double Height { get; }

            public List<ItemLayout> Items { get; }
        }

        private sealed class ItemLayout
        {
            public ItemLayout(UIElement child, double width, double height)
            {
                Child = child;
                Width = width;
                Height = height;
            }

            public UIElement Child { get; }

            public double Width { get; }

            public double Height { get; }
        }

        private sealed class PendingItemLayout
        {
            public PendingItemLayout(UIElement child, double width)
            {
                Child = child;
                Width = width;
            }

            public UIElement Child { get; }

            public double Width { get; }
        }
    }
}
