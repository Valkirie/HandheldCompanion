using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HandheldCompanion.Converters
{
    public class ColorToBrushConverter : IValueConverter
    {
        // Color (or SolidColorBrush) → Brush
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            if (value is SolidColorBrush brush)
            {
                return brush;
            }
            return DependencyProperty.UnsetValue;
        }

        // Brush → Color
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return DependencyProperty.UnsetValue;
        }
    }
}
