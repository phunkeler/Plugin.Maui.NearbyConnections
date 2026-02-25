namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One-time app configuration for Nearby Connections.
/// </summary>
public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the name to display when advertising/discovering.
    /// Defaults to <see cref="DeviceInfo.Name"/>.
    /// </summary>
    public string DisplayName
    {
        get => field;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = DeviceInfo.Name;

    /// <summary>
    /// Gets or sets the service identifer used to discover and connect with nearby devices.
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
    /// <exception cref="ArgumentNullException">Thrown when setting a null value.</exception>
    /// <exception cref="ArgumentException">Thrown when setting an empty string or whitespace value.</exception>
    public string ServiceId
    {
        get => field;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = AppInfo.Name;

    /// <summary>
    /// Gets or sets a value indicating that incoming connection requests, from nearby discoverers, should automatically be accepted.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When set to <see langword="false"/>, the <see cref="INearbyConnections.RespondToConnectionAsync"/>
    /// method must be called to accept or reject the request.
    /// </remarks>
    public bool AutoAcceptConnections { get; set; } = true;

    /// <summary>
    /// Gets or sets the directory where received files are saved after transfer.
    /// Defaults to <see cref="FileSystem.CacheDirectory"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting a null value.</exception>
    /// <exception cref="ArgumentException">Thrown when setting an empty string or whitespace value.</exception>
    public string ReceivedFilesDirectory
    {
        get => field;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = FileSystem.CacheDirectory;

    /// <summary>
    /// Gets or sets the maximum time to wait without receiving a transfer progress update
    /// before considering a data transfer stalled and aborting it.
    /// Defaults to 10 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan TransferInactivityTimeout { get; set; } = TimeSpan.FromSeconds(10);
}