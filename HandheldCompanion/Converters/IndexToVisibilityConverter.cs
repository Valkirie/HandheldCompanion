using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    public sealed class IndexToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Convert bool or Nullable bool to Visibility
        /// </summary>
        /// <param name="value">int</param>
        /// <param name="targetType">Visibility</param>
        /// <param name="parameter">expected int</param>
        /// <param name="culture">null</param>
        /// <returns>Visible or Collapsed</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string sValue = System.Convert.ToString(value);
            string sParameter = System.Convert.ToString(parameter);

            if (!string.IsNullOrEmpty(sParameter))
            {
                string[] parameters = sParameter.Split(new char[] { '|' });
                foreach (string idx in parameters)
                    if (idx.Equals(sValue))
                        return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        /// <summary>
        /// Convert Visibility to boolean
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility)
            {
                return (Visibility)value == Visibility.Visible;
            }
            else
            {
                return false;
            }
        }
    }
}
