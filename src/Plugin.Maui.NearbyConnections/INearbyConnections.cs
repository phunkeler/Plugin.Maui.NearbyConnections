namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Interface defining the Nearby Connections functionality.
/// </summary>
public interface INearbyConnections : IDisposable
{
    /// <summary>
    /// Gets the events related to nearby connections.
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
    /// Gets a value indicating whether this device is currently advertising to nearby devices.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently discovering nearby devices.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Starts advertising this device to nearby discoverers.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation of starting advertising.</returns>
    /// <exception cref="NearbyAdvertisingException">Thrown if advertising fails to start.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    Task StartAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start discovering nearby devices.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation of starting discovery.</returns>
    /// <exception cref="NearbyDiscoveryException">Thrown if discovery fails to start.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
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