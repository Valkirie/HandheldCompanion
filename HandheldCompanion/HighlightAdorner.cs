using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HandheldCompanion
{
    public class HighlightAdorner : Adorner
    {
        private readonly Rectangle _rectangle;
        private readonly FrameworkElement _element;

        public HighlightAdorner(UIElement adornedElement) : base(adornedElement)
        {
            // We need FrameworkElement for ActualWidth/Height, SizeChanged, etc.
            _element = adornedElement as FrameworkElement
                       ?? throw new InvalidOperationException("HighlightAdorner can only be applied to a FrameworkElement");

            // Get the FocusVisualMargin
            Thickness focusVisualMargin = FocusVisualHelper.GetFocusVisualMargin(_element);

            _rectangle = new Rectangle
            {
                StrokeThickness = 2,
                RadiusX = 2,
                RadiusY = 2,
                Fill = Brushes.Transparent,
                Width = adornedElement.RenderSize.Width,
                Height = adornedElement.RenderSize.Height,
                Margin = focusVisualMargin,
            };

            // Bind the Stroke property to a dynamic resource
            _rectangle.SetResourceReference(Shape.StrokeProperty, "SystemControlForegroundBaseHighBrush");

            // Apply the initial corner radius
            UpdateCornerRadius();

            // workaround
            if (_element is ToggleSwitch toggleSwitch)
            {
                this.Margin = new Thickness(toggleSwitch.DesiredSize.Height, 0, 0, 0);
                _rectangle.Width = toggleSwitch.DesiredSize.Width;
            }

            this.AddVisualChild(_rectangle);

            // prevent adorner from catching click
            IsHitTestVisible = false;

            // prevent adorner from catching drag & drop
            ScrollViewer.SetPanningMode(this, PanningMode.HorizontalOnly);

            // Re-apply size & corner radius whenever the adorned element changes
            _element.SizeChanged += AdornedElement_SizeChanged;
        }

        private void AdornedElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _rectangle.Width = e.NewSize.Width;
            _rectangle.Height = e.NewSize.Height;
            UpdateCornerRadius();
            InvalidateArrange();
        }

        private void UpdateCornerRadius()
        {
            double radius = 2; // fallback

            // If the element is a Border, grab its CornerRadius
            if (_element is Border border)
            {
                radius = border.CornerRadius.TopLeft;
            }
            // If it's a WinUI/Fluent ToggleSwitch, use its CornerRadius
            else if (_element is ToggleSwitch ts)
            {
                radius = ts.CornerRadius.TopLeft;
            }
            // If it's some other Control with a CornerRadius property in its template...
            else if (_element is Control ctrl)
            {
                // try to find the named border in its template
                var templateBorder = ctrl.Template?.FindName("Border", ctrl) as Border
                                    ?? ctrl.Template?.FindName("ContainerBorder", ctrl) as Border;
                if (templateBorder != null)
                    radius = templateBorder.CornerRadius.TopLeft;
            }

            _rectangle.RadiusX = radius;
            _rectangle.RadiusY = radius;
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _rectangle;

        protected override Size ArrangeOverride(Size finalSize)
        {
            _rectangle.Arrange(new Rect(new Point(0, 0), AdornedElement.RenderSize));
            return finalSize;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            return new Size(_rectangle.DesiredSize.Width, _rectangle.DesiredSize.Height);
        }
    }
}
