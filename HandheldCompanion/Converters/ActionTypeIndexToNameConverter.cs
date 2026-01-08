using HandheldCompanion.Properties;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class ActionTypeIndexToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            return index switch
            {
                0 => Resources.LayoutPage_ActionType_Disabled,
                1 => Resources.LayoutPage_ActionType_Button,
                2 => Resources.LayoutPage_ActionType_Joystick,
                3 => Resources.LayoutPage_ActionType_Keyboard,
                4 => Resources.LayoutPage_ActionType_Mouse,
                5 => Resources.LayoutPage_ActionType_Trigger,
                6 => Resources.LayoutPage_ActionType_Shift,
                7 => Resources.LayoutPage_ActionType_Inherit,
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

