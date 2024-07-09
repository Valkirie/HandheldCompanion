using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    public class SeparatorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue)
                return null;

            if (values[1] == DependencyProperty.UnsetValue)
                return null;

            string text = values[0] as string;
            bool isEnabled = (bool)values[1];

            ComboBoxItem comboBoxItem = new ComboBoxItem() { IsEnabled = isEnabled, Margin = new(0), Padding = new(0) };

            if (string.IsNullOrEmpty(text))
                comboBoxItem.Content = new Separator();
            else
                return text;

            return comboBoxItem;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
