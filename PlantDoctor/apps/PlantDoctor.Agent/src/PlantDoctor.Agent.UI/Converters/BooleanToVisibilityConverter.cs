using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlantDoctor.Agent.UI.Converters;

/// <summary>
/// Converts a boolean value to Visibility (true → Visible, false → Collapsed).
/// Supports ConverterParameter="Reverse" to invert the logic.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool reverse = parameter?.ToString()?.ToLower() == "reverse";
            var isVisible = reverse ? !boolValue : boolValue;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool reverse = parameter?.ToString()?.ToLower() == "reverse";
            return (visibility == Visibility.Visible) != reverse;
        }
        return false;
    }
}
