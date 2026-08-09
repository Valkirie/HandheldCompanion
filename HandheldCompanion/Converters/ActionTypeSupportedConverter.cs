using HandheldCompanion.Actions;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public sealed class ActionTypeSupportedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ActionType[] supportedActionTypes
            && parameter is ActionType actionType
            && supportedActionTypes.Contains(actionType);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
