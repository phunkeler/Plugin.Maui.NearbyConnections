namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One-time app configuration for Nearby Connections.
/// </summary>
public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets or sets the display name for this device when advertising and connecting with nearby devices.
    /// </summary>
    public string DisplayName
    {
        get => field;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = DeviceInfo.Current.Name;

    /// <summary>
    /// Gets or sets the service identifer used to discover and connect with nearby devices.
    /// The default value is the application name (e.g., <see href="https://learn.microsoft.com/en-us/dotnet/api/microsoft.maui.applicationmodel.iappinfo.name">Microsoft.Maui.ApplicationModel.IAppInfo.Name</see>).
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
    } = AppInfo.Current.Name;

    /// <summary>
    /// When a nearby device requests a connection, automatically accept it.
    /// </summary>
    public bool AutoAcceptConnections { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum time to wait without receiving a transfer progress update
    /// before considering a data transfer stalled and aborting it.
    /// Defaults to 10 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan TransferInactivityTimeout { get; set; } = TimeSpan.FromSeconds(10);
}