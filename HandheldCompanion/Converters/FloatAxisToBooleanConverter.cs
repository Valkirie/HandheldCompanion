using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class FloatAxisToBooleanConverter : IValueConverter
{
    /// <summary>
    /// Converts float axis value (-1 or 1) to boolean.
    /// Returns true if value is -1 (inverted), false if value is 1 (normal).
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float floatValue)
            return floatValue == -1f;
        return false;
    }

    /// <summary>
    /// Converts boolean back to float axis value.
    /// Returns -1 if checked (inverted), 1 if unchecked (normal).
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? -1f : 1f;
        return 1f;
    }
}
