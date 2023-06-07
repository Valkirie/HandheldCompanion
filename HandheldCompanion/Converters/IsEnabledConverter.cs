using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public sealed class IsEnabledConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        foreach (var value in values)
            switch (value)
            {
                case bool b when !b:
                case int i when i == 0:
                    return false;
            }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return null;
    }
}