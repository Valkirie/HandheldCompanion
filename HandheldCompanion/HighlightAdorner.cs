using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HandheldCompanion
{
    public class HighlightAdorner : Adorner
    {
        private Rectangle _rectangle;

        public HighlightAdorner(UIElement adornedElement) : base(adornedElement)
        {
            // Get the FocusVisualMargin
            Thickness focusVisualMargin = FocusVisualHelper.GetFocusVisualMargin((FrameworkElement)adornedElement);

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

            // workaround
            if (adornedElement is ToggleSwitch toggleSwitch)
            {
                this.Margin = new(toggleSwitch.DesiredSize.Height, 0, 0, 0);
                _rectangle.Width = toggleSwitch.DesiredSize.Width;
            }

            this.AddVisualChild(_rectangle);

            // prevent adorner from catching click
            IsHitTestVisible = false;
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
            _rectangle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return new Size(_rectangle.DesiredSize.Width, _rectangle.DesiredSize.Height);
        }
    }
}
