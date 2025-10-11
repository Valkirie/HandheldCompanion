using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    public class WidthToColumnsConverter : IValueConverter
    {
        public double DesiredItemWidth { get; set; } = 100; // width of one cell

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && width > 0)
                return Math.Max(1, (int)(width / DesiredItemWidth));

            return 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
