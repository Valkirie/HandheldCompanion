using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class PercentageConverter : IValueConverter
{
    public object Convert(object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var d_value = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var d_parameter = System.Convert.ToDouble(parameter, CultureInfo.InvariantCulture);

        return d_value * d_parameter;
    }

    public object ConvertBack(object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}