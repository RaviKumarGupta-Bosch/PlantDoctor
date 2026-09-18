using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PlantDoctor.Agent.UI.Converters;

/// <summary>
/// Converts a boolean resolved state to a background color.
/// True (resolved) → Light Green (#E8F5E9)
/// False (unresolved) → Light Red (#FFF5F5)
/// </summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public class ResolvedBackgroundConverter : IValueConverter
{
    private static readonly Brush ResolvedBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9));
    private static readonly Brush UnresolvedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xF5, 0xF5));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isResolved)
        {
            return isResolved ? ResolvedBrush : UnresolvedBrush;
        }
        return UnresolvedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
