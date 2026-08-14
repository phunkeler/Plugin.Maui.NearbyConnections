namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Identifies the kind of device event yielded by the internal discovery stream.
/// </summary>
enum NearbyDeviceEventType
{
    /// <summary>A nearby device has been discovered and is advertising.</summary>
    Found,

    /// <summary>A previously discovered nearby device is no longer visible.</summary>
    Lost,
}