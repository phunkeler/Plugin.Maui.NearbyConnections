using System.Globalization;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Converters;

static class NearbyDeviceStateConverter
{
    public static string ToGlyph(NearbyDeviceState state) => state switch
    {
        NearbyDeviceState.Discovered                  => Resource<string>("icon-magnify"),
        NearbyDeviceState.ConnectionRequestedInbound  => Resource<string>("icon-down"),
        NearbyDeviceState.ConnectionRequestedOutbound => Resource<string>("icon-up"),
        NearbyDeviceState.Connected                   => Resource<string>("icon-check"),
        _ => string.Empty
    };

    public static Color ToColor(NearbyDeviceState state) => state switch
    {
        NearbyDeviceState.Discovered                  => Resource<Color>("LightTextQuaternary"),
        NearbyDeviceState.ConnectionRequestedInbound  => Resource<Color>("LightTextQuaternary"),
        NearbyDeviceState.ConnectionRequestedOutbound => Resource<Color>("LightTextQuaternary"),
        NearbyDeviceState.Connected                   => Resource<Color>("StatusSuccess"),
        _ => Colors.Transparent
    };

    static T Resource<T>(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var value) && value is T typed
            ? typed
            : default!;
}

public class NearbyDeviceStateGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NearbyDeviceState state ? NearbyDeviceStateConverter.ToGlyph(state) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null!;
}

public class NearbyDeviceStateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NearbyDeviceState state ? NearbyDeviceStateConverter.ToColor(state) : Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null!;
}
