namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A single device-discovery event yielded by the internal discovery stream.
/// </summary>
/// <remarks>
/// Internal: the session applies these to <see cref="INearbySession.Devices"/>, which is what
/// consumers observe.
/// </remarks>
sealed class NearbyDeviceEvent
{
    /// <summary>
    /// Gets the device that was found or lost.
    /// </summary>
    public NearbyDevice Device { get; }

    /// <summary>
    /// Gets the type of event: <see cref="NearbyDeviceEventType.Found"/> when the device
    /// becomes visible, or <see cref="NearbyDeviceEventType.Lost"/> when it disappears.
    /// </summary>
    public NearbyDeviceEventType Type { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="NearbyDeviceEvent"/>.
    /// </summary>
    /// <param name="device">The device involved in the event.</param>
    /// <param name="type">Whether the device was found or lost.</param>
    public NearbyDeviceEvent(NearbyDevice device, NearbyDeviceEventType type)
    {
        Device = device;
        Type = type;
    }
}
