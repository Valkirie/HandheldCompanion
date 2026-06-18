using HandheldCompanion.Utils;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class IntToMotionInputConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MotionInput mode)
            return (int)mode;
        if (value is int index)
            return (MotionInput)index;
        return (int)MotionInput.LocalSpace;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MotionInput mode)
            return (int)mode;
        if (value is int index)
            return (MotionInput)index;
        return (int)MotionInput.LocalSpace;
    }
}
