using System.Globalization;

namespace NearbyChat.Converters;

public class LocalTimeConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is DateTimeOffset dateTimeOffset)
            return dateTimeOffset.ToLocalTime().ToString("t", culture);

        if (value is DateTime dateTime)
            return dateTime.ToLocalTime().ToString("t", culture);

        return string.Empty;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => null!;
}
