using HandheldCompanion.Properties;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class ActionDisplayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return "Unknown";

        string actionType = values[0]?.ToString() ?? "Unknown";

        // For Inherit action type, just return "Inherit" without target
        // Compare with the localized resource string
        string inheritString = Resources.LayoutPage_ActionType_Inherit;
        if (actionType.Equals(inheritString, StringComparison.OrdinalIgnoreCase))
            return inheritString;

        // Check if target is null or DependencyProperty.UnsetValue
        object targetValue = values[1];
        if (targetValue == null || targetValue == DependencyProperty.UnsetValue)
            return actionType;

        string target = targetValue.ToString() ?? "None";

        // If target is "None" or empty, just return the action type
        if (string.IsNullOrWhiteSpace(target) || target.Equals("None", StringComparison.OrdinalIgnoreCase))
            return actionType;

        return $"{actionType}: {target}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

