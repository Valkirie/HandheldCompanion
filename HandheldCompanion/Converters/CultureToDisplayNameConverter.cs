using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    class CultureToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CultureInfo sourceCulture)
            {
                return sourceCulture.NativeName;
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
