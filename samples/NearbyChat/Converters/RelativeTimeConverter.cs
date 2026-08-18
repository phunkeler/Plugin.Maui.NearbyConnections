using System.Globalization;

namespace NearbyChat.Converters;

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        TimeSpan span;

        if (value is DateTimeOffset dateTimeOffset)
        {
            span = DateTimeOffset.Now - dateTimeOffset;
        }
        else if (value is DateTime dateTime)
        {
            span = DateTime.Now - dateTime;
        }
        else
        {
            return string.Empty;
        }

        return span.TotalSeconds switch
        {
            < 60 => "Just now",
            < 3600 => $"{(int)span.TotalMinutes} min ago",
            < 86400 => $"{(int)span.TotalHours}h ago",
            _ => string.Empty,
        };
    }

    // One-way binding only: a formatted timestamp cannot be parsed back to the instant it came
    // from, so a caller asking for that has a defect rather than a missing feature.
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => throw new NotSupportedException();
}
