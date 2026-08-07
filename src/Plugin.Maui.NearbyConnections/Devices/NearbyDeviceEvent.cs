namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A single device-discovery event yielded by the internal discovery stream.
/// </summary>
/// <param name="Device">The device that was found or lost.</param>
/// <param name="Type">
/// <see cref="NearbyDeviceEventType.Found"/> when the device becomes visible, or
/// <see cref="NearbyDeviceEventType.Lost"/> when it disappears.
/// </param>
/// <remarks>
/// Internal: the session applies these to <see cref="INearbySession.Devices"/>, which is what
/// consumers observe.
/// </remarks>
sealed record NearbyDeviceEvent(NearbyDevice Device, NearbyDeviceEventType Type);
