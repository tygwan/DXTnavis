using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DXTnavis.Converters
{
    /// <summary>
    /// Boolean 값을 WPF Visibility로 변환하는 컨버터
    /// true -> Visible, false -> Collapsed
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }

            return false;
        }
    }
}
