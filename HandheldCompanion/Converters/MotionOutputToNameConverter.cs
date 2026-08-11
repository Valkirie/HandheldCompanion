using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class MotionOutputToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            return index switch
            {
                0 => "Disabled",
                1 => "Left Stick",
                2 => "Right Stick",
                3 => "Move Cursor",
                4 => "Scroll Wheel",
                5 => "Left Pad",
                6 => "Right Pad",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
