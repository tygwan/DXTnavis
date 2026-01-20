using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DXTnavis.Converters
{
    /// <summary>
    /// Boolean 값을 WPF Visibility로 변환하는 컨버터
    /// true -> Visible, false -> Collapsed
    /// ConverterParameter="Invert" 시 반전 (true -> Collapsed, false -> Visible)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Invert 파라미터 체크
                bool invert = parameter is string paramStr &&
                              paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase);

                if (invert)
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool invert = parameter is string paramStr &&
                              paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase);

                bool result = visibility == Visibility.Visible;
                return invert ? !result : result;
            }

            return false;
        }
    }

    /// <summary>
    /// Boolean 값을 반전하는 컨버터 (Phase 10)
    /// true -> false, false -> true
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return false;
        }
    }
}
