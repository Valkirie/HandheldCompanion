using HandheldCompanion.Managers;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.Converters;

/// <summary>
/// Converts a BitmapImage to Visibility based on whether it's valid artwork or a placeholder.
/// Returns Collapsed if the image is null, MissingCover, MissingArtwork or empty.
/// </summary>
public sealed class HasArtworkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not BitmapImage image)
            return Visibility.Collapsed;

        // Check if it's the MissingCover placeholder
        if (image == LibraryResources.MissingCover || image == LibraryResources.MissingArtwork)
            return Visibility.Collapsed;

        // Check if UriSource is null or empty
        if (image.UriSource == null)
            return Visibility.Collapsed;

        // Check if it's pointing to the MissingCover resource
        string uri = image.UriSource.ToString();
        if (uri.Contains("MissingCover.png", StringComparison.OrdinalIgnoreCase))
            return Visibility.Collapsed;
        else if (uri.Contains("MissingArtwork.png", StringComparison.OrdinalIgnoreCase))
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
