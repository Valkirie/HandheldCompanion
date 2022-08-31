using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture)
        {
            double d_value = System.Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            double d_parameter = System.Convert.ToDouble(parameter, System.Globalization.CultureInfo.InvariantCulture);

            return d_value * d_parameter;
        }

        public object ConvertBack(object value,
            Type targetType,
            object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
