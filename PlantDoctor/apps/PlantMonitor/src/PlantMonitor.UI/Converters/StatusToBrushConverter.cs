using System.Windows.Data;
using System.Windows.Media;
using PlantDoctor.Contracts;

namespace PlantMonitor.UI.Converters;

/// <summary>Converts a channel/reading status string to a SolidColorBrush (green/amber/red).</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var status = value as string ?? ChannelStatus.Stopped;
        return status switch
        {
            "OK" or ChannelStatus.Connected => Brushes.Green,
            "Warning" or ChannelStatus.Listening => Brushes.Orange,
            "Critical" or ChannelStatus.Error => Brushes.Red,
            ChannelStatus.Stopped or ChannelStatus.Closed => Brushes.Gray,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
