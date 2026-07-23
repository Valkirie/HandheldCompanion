using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public sealed class CharToComboBoxIndexConverter : IValueConverter
{
    /// <summary>
    ///     Convert char (X, Y, Z) to ComboBox SelectedIndex (0, 1, 2)
    /// </summary>
    /// <param name="value">char (X, Y, or Z)</param>
    /// <param name="targetType">int</param>
    /// <param name="parameter">Unused</param>
    /// <param name="culture">Culture info</param>
    /// <returns>0 for X, 1 for Y, 2 for Z</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is char c)
        {
            return c switch
            {
                'X' => 0,
                'Y' => 1,
                'Z' => 2,
                _ => 0
            };
        }
        return 0;
    }

    /// <summary>
    ///     Convert ComboBox SelectedIndex (0, 1, 2) back to char (X, Y, Z)
    /// </summary>
    /// <param name="value">int (0, 1, or 2)</param>
    /// <param name="targetType">char</param>
    /// <param name="parameter">Unused</param>
    /// <param name="culture">Culture info</param>
    /// <returns>X, Y, or Z</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            return index switch
            {
                0 => 'X',
                1 => 'Y',
                2 => 'Z',
                _ => 'X'
            };
        }
        return 'X';
    }
}
