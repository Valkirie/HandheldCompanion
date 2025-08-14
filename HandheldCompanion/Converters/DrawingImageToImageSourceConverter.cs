using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.Converters
{
    public class DrawingImageToImageSourceConverter : IValueConverter
    {
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DeleteObject(IntPtr hObject);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Drawing.Image img)
            {
                var hBmp = ((Bitmap)img).GetHbitmap();
                try
                {
                    var bs = Imaging.CreateBitmapSourceFromHBitmap(
                        hBmp, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(48, 48));
                    bs.Freeze();
                    return bs;
                }
                finally
                {
                    DeleteObject(hBmp);
                }
            }
            return null;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
