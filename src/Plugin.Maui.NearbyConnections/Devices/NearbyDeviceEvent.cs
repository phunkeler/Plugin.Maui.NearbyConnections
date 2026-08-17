namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A single device-discovery event yielded by the internal discovery stream.
/// </summary>
/// <param name="Device">The device that was found or lost.</param>
/// <param name="Found">
/// <see langword="true"/> when the device becomes visible, or <see langword="false"/> when it
/// disappears.
/// </param>
/// <remarks>
/// Internal: the session applies these to <see cref="INearby.Devices"/>, which is what
/// consumers observe. Discovery is a two-state fact — a device is either found or lost — so this
/// carries a <see cref="bool"/> rather than a two-case enum, which spared a second file and a
/// <c>default:</c> arm that could never run.
/// </remarks>
sealed record NearbyDeviceEvent(NearbyDevice Device, bool Found);
