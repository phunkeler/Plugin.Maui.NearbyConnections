namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Main interface for nearby connections functionality.
/// Provides centralized event handling and device management.
/// </summary>
public interface INearbyConnections : IDisposable
{
    /// <summary>
    /// An observable stream of processed events from the Nearby Connections API.
    /// Events flow through internal handlers before being exposed externally.
    /// </summary>
    NearbyConnectionsEvents Events { get; }

    /// <summary>
    /// Gets or sets the options used to configure the Nearby Connections functionality.
    /// </summary>
    NearbyConnectionsOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the display name shown to nearby devices during advertising.
    /// Can be changed between advertising sessions.
    /// Changes take effect on next <see cref="StartAdvertisingAsync"/> call.
    /// </summary>
    string DisplayName { get; set; }

    /// <summary>
    /// Start advertising this device.
    /// </summary>
    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start discovering nearby devices.
    /// </summary>
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an invitation to connect to the specified <see cref="NearbyDevice"/>.
    /// </summary>
    /// <param name="device">The device to which the invitation will be sent. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation of sending the invitation.</returns>
    Task RequestConnectionAsync(NearbyDevice device);

    /// <summary>
    /// Respond to a connection request from the specified <see cref="NearbyDevice"/>.
    /// </summary>
    /// <param name="device">The device that sent the connection request.</param>
    /// <param name="accept"><see langword="true"/> to accept the connection; <see langword="false"/> to decline.</param>
    /// <returns>A task that represents the asynchronous operation of responding to the connection request.</returns>
    Task RespondToConnectionAsync(NearbyDevice device, bool accept);
}