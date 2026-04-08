using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

/// <summary>
/// Converts a width value to a height based on aspect ratio with min/max constraints.
/// Used for responsive artwork banners that scale with page width.
/// </summary>
public sealed class WidthToAspectHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || width <= 0)
            return 300.0; // Default fallback

        double ratio = 0.428; // 21:9
        if (parameter is string paramStr && double.TryParse(paramStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRatio))
            ratio = parsedRatio;

        // Calculate height using the specified aspect ratio
        return width * ratio;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
