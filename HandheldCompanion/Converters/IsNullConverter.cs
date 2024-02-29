using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class IsNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue == 0;

        return value is null;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new InvalidOperationException("IsNullConverter can only be used OneWay.");
    }
}