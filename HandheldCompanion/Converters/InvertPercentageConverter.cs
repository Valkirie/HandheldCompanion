using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class InvertPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return 100 - (double)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return 100 - (double)value;
    }
}