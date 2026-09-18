using System.Globalization;
using System.Windows.Data;

namespace PlantDoctor.Agent.UI.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public sealed class BooleanInverterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool boolean && !boolean;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool boolean && !boolean;
}