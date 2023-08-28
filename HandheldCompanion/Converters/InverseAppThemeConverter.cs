﻿using Inkore.UI.WPF.Modern;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HandheldCompanion.Converters;

public class InverseAppThemeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        switch ((ApplicationTheme)value)
        {
            case ApplicationTheme.Light:
                return ElementTheme.Dark;
            case ApplicationTheme.Dark:
                return ElementTheme.Light;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}