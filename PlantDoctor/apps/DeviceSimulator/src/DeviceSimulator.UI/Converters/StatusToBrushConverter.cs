using System.Windows.Data;
using System.Windows.Media;
using PlantDoctor.Contracts;

namespace DeviceSimulator.UI.Converters;

/// <summary>Converts a device/channel status string to a SolidColorBrush (green/amber/red).</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var status = value as string ?? ChannelStatus.Closed;
        return status switch
        {
            "OK" or ChannelStatus.Connected => Brushes.Green,
            "Warning" or ChannelStatus.Listening => Brushes.Orange,
            "Critical" or ChannelStatus.Error => Brushes.Red,
            ChannelStatus.Closed or ChannelStatus.Stopped => Brushes.Gray,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
