using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace HandheldCompanion.Converters
{
    // Filters to only items at even positions (0-based)
    public class EvenIndexFilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable seq)
            {
                // Enumerate with index and take even indices
                return seq.Cast<object>()
                          .Select((item, idx) => new { item, idx })
                          .Where(x => x.idx % 2 == 0)
                          .Select(x => x.item)
                          .ToList();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    // Filters to only items at odd positions
    public class OddIndexFilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable seq)
            {
                return seq.Cast<object>()
                          .Select((item, idx) => new { item, idx })
                          .Where(x => x.idx % 2 == 1)
                          .Select(x => x.item)
                          .ToList();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
