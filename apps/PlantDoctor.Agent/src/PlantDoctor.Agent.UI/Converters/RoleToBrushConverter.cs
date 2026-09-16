using System.Windows.Data;
using System.Windows.Media;

namespace PlantDoctor.Agent.UI.Converters;

/// <summary>
/// Converts chat role to background brush color.
/// </summary>
public class RoleToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string role)
        {
            return role.ToLower() switch
            {
                "operator" => new SolidColorBrush(Color.FromRgb(240, 248, 255)), // Light blue
                "assistant" => new SolidColorBrush(Color.FromRgb(245, 255, 250)), // Light green
                _ => new SolidColorBrush(Colors.White)
            };
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
