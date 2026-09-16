using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PlantDoctor.Agent.UI.Converters;

/// <summary>
/// Converts log level strings to brush colors.
/// </summary>
public class LevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string level)
        {
            return level.ToLower() switch
            {
                "critical" => Brushes.Red,
                "error" => Brushes.OrangeRed,
                "warning" => Brushes.Orange,
                "info" => Brushes.DarkGreen,
                _ => Brushes.Black
            };
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
