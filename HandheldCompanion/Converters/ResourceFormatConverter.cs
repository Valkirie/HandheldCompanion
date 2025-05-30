using HandheldCompanion.Localization;

using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    public class ResourceFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0) return string.Empty;
            string formatString = TranslationSource.Instance[parameter.ToString() ?? ""];
            return string.Format(formatString, values);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
