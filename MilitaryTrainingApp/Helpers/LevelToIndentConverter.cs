using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MilitaryTrainingApp.Helpers
{
    public class LevelToIndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                // indent = (level - 1) * 20 + 5
                double leftIndent = (level - 1) * 20 + 5;
                return new Thickness(leftIndent, 0, 0, 0);
            }
            return new Thickness(5, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
