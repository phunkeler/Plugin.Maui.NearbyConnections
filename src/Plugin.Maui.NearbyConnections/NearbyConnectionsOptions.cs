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
    public string DisplayName { get; init; } = DeviceInfo.Name;

    /// <summary>
    /// Gets the service identifier used to discover and connect with nearby devices.
    /// Defaults to <see cref="AppInfo.Name"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On Android, this is used as the <c>serviceId</c> when advertising/discovery Google Play Service's Nearby Connections API
    /// (<see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/package-summary">developers.google.com</see>).
    /// </para>
    /// <para>
    /// On iOS, this is a Bonjour service type defined in the application's Info.plist
    /// (<see href="https://developer.apple.com/documentation/BundleResources/Information-Property-List/NSBonjourServices">developer.apple.com</see>).
    /// </para>
    /// </remarks>
    public string ServiceId { get; init; } = AppInfo.Name;

    /// <summary>
    /// Gets a value indicating that incoming connection requests, from nearby discoverers, should automatically be accepted.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When set to <see langword="false"/>, the <see cref="INearbyConnections.RespondToConnectionAsync"/>
    /// method must be called to accept or reject the request.
    /// </remarks>
    public bool AutoAcceptConnections { get; init; } = true;

    /// <summary>
    /// Gets the directory where received files are saved after transfer.
    /// Defaults to <see cref="FileSystem.CacheDirectory"/>.
    /// </summary>
    public string ReceivedFilesDirectory { get; init; } = FileSystem.CacheDirectory;

    /// <summary>
    /// Gets the maximum time to wait without receiving a transfer progress update
    /// before considering a data transfer stalled and aborting it.
    /// Defaults to 10 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan TransferInactivityTimeout { get; init; } = TimeSpan.FromSeconds(10);
}