namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One-time startup configuration for Nearby Connections. All properties are set once
/// via <c>AddNearbyConnections</c> and cannot be changed after initialization.
/// </summary>
public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets the name to display when advertising/discovering.
    /// Defaults to <see cref="DeviceInfo.Name"/>.
    /// </summary>
    public string DisplayName { get; set; } = GetDefaultDisplayName();

    /// <summary>
    /// Gets the service identifier used to discover and connect with nearby devices.
    /// On Android, defaults to <see cref="AppInfo.Name"/>. On iOS, this property has no default
    /// and <b>must</b> be set explicitly before calling <c>AdvertiseAsync</c> or <c>DiscoverAsync</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On Android, this is used as the <c>serviceId</c> when advertising/discovery Google Play Service's Nearby Connections API
    /// (<see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/package-summary">developers.google.com</see>).
    /// </para>
    /// <para>
    /// On iOS, this must be a Bonjour service type in the form <c>_&lt;name&gt;._tcp</c> or
    /// <c>_&lt;name&gt;._udp</c> (for example <c>_mygame._tcp</c>), matching the entry declared in the
    /// application's <c>Info.plist</c> under <c>NSBonjourServices</c>
    /// (<see href="https://developer.apple.com/documentation/BundleResources/Information-Property-List/NSBonjourServices">developer.apple.com</see>).
    /// There is no meaningful default on iOS; app startup will fail if this property is not set.
    /// </para>
    /// </remarks>
    public string ServiceId { get; set; } = GetDefaultServiceId();

    /// <summary>
    /// Gets a value indicating that incoming connection requests, from nearby discoverers, should automatically be accepted.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see langword="false"/> (the default), each <see cref="NearbyConnectionRequest"/> yielded by
    /// <c>AdvertiseAsync</c> must be explicitly accepted or rejected by the caller.
    /// Call <see cref="NearbyConnectionRequest.RejectAsync"/> to reject.
    /// </para>
    /// <para>
    /// When <see langword="true"/>, the platform automatically accepts every inbound request without
    /// any consumer code running. Only set this if you control all advertising and discovering devices
    /// and trust every peer that may discover you.
    /// </para>
    /// </remarks>
    public bool AutoAcceptConnections { get; set; }

    /// <summary>
    /// Gets the directory where received files are saved after transfer.
    /// Defaults to <see cref="FileSystem.AppDataDirectory"/> (persistent storage).
    /// </summary>
    public string ReceivedFilesDirectory { get; set; } = GetDefaultReceivedFilesDirectory();

    private static partial string GetDefaultDisplayName();
    private static partial string GetDefaultServiceId();
    private static partial string GetDefaultReceivedFilesDirectory();

    /// <summary>
    /// Gets the maximum time to wait without receiving a transfer progress update
    /// before considering a data transfer stalled and aborting it.
    /// Defaults to 10 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan TransferInactivityTimeout { get; set; } = TimeSpan.FromSeconds(10);

}