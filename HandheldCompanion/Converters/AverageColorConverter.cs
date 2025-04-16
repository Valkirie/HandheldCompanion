using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.Converters
{
    public class AverageColorConverter : IValueConverter
    {
        // Note: This is a simplified example.
        // In a real-world scenario, you’d need to handle errors,
        // image formats, and potentially optimize for performance.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BitmapSource bitmap)
            {
                // Create a scaled down version to reduce processing load.
                var scaled = new TransformedBitmap(bitmap, new ScaleTransform(0.1, 0.1));
                int width = scaled.PixelWidth;
                int height = scaled.PixelHeight;
                int stride = width * 4;
                byte[] pixelData = new byte[height * stride];
                scaled.CopyPixels(pixelData, stride, 0);

                long r = 0, g = 0, b = 0, pixels = width * height;
                for (int i = 0; i < pixelData.Length; i += 4)
                {
                    b += pixelData[i];
                    g += pixelData[i + 1];
                    r += pixelData[i + 2];
                }
                byte avgR = (byte)(r / pixels);
                byte avgG = (byte)(g / pixels);
                byte avgB = (byte)(b / pixels);

                Color original = new Color()
                {
                    R = avgR,
                    G = avgG,
                    B = avgB,
                };

                // lighter
                return Color.FromArgb((byte)(original.A),
                                    (byte)(original.R + (255 - original.R) * 0.25),
                                    (byte)(original.G + (255 - original.G) * 0.25),
                                    (byte)(original.B + (255 - original.B) * 0.25));
            }

            // Fallback color if the conversion fails
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
