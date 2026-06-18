using HandheldCompanion.Utils;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class IntToMotionOutputConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MotionOutput mode)
            return (int)mode;
        if (value is int index)
            return (MotionOutput)index;
        return MotionOutput.Disabled;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MotionOutput mode)
            return (int)mode;
        if (value is int index)
            return (MotionOutput)index;
        return (int)MotionOutput.Disabled;
    }
}
