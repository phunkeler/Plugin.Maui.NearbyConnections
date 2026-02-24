using System.Globalization;

namespace NearbyChat.Converters;

/// <summary>
/// Converts a boolean connection state to a color for the connection indicator.
/// </summary>
public class BoolToConnectionColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isConnected)
        {
            return isConnected ? Colors.Green : Colors.Red;
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}