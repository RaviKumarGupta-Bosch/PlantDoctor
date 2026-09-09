using System.Windows.Data;
using System.Windows.Media;

namespace PlantSimulator.UI.Converters;

/// <summary>Converts sensor status string to a SolidColorBrush (green/amber/red).</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public static readonly StatusToBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var status = value as string ?? "OK";
        return status switch
        {
            "OK" or "Connected" => Brushes.Green,
            "Warning" or "Disconnected" => Brushes.Orange,
            "Critical" or "Error" => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}
