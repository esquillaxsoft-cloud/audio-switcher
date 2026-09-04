using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Esquillax.AudioSwitcher.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool b)
            {
                flag = b;
            }

            if (Invert)
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool isVisible = visibility == Visibility.Visible;
                return Invert ? !isVisible : isVisible;
            }
            return false;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNotNull = value != null;
            if (value is string s)
            {
                isNotNull = !string.IsNullOrWhiteSpace(s);
            }

            if (Invert)
            {
                isNotNull = !isNotNull;
            }

            return isNotNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
