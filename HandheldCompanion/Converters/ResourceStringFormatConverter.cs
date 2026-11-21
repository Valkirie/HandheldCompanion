using HandheldCompanion.Localization;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    /// <summary>
    /// Formats a single bound value with a localized format string, the resource key is the converter parameter.
    /// Like Binding.StringFormat, but the format comes from Resources.resx.
    /// </summary>
    public class ResourceStringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string formatString = TranslationSource.Instance[parameter?.ToString() ?? string.Empty];
            return string.Format(culture, formatString, value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
