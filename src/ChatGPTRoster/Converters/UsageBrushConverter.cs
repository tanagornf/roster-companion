using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChatGPTRoster.Converters;

public sealed class UsageBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Healthy = new(Color.FromRgb(18, 163, 109));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(247, 144, 9));
    private static readonly SolidColorBrush Critical = new(Color.FromRgb(217, 45, 32));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var remaining = value is double number ? number : 0;
        return remaining switch
        {
            > 40 => Healthy,
            > 15 => Warning,
            _ => Critical
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
