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
        {
            return dateTimeOffset.ToLocalTime().ToString("t", culture);
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString("t", culture);
        }

        return string.Empty;
    }

    // One-way binding only: a formatted timestamp cannot be parsed back to the instant it came
    // from, so a caller asking for that has a defect rather than a missing feature.
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
