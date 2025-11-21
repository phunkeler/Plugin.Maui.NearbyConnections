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
    IObservable<INearbyConnectionsEvent> Events { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently advertising.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently discovering nearby devices.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Gets all nearby devices regardless of <see cref="NearbyDeviceStatus"/>.
    /// </summary>
    IReadOnlyDictionary<string, NearbyDevice> Devices { get; }

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
    /// Begin discovery of <see cref="NearbyDevice"/>'s.
    /// </summary>
    Task StartDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    Task StopAdvertisingAsync();

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    Task StopDiscoveryAsync();

    /// <summary>
    /// Sends an invitation to the specified nearby device asynchronously.
    /// </summary>
    /// <param name="device">The device to which the invitation will be sent. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the invitation operation.</param>
    /// <returns>A task that represents the asynchronous operation of sending the invitation.</returns>
    Task SendInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept the invitation to connect with the provided <see cref="NearbyDevice"/> .
    /// </summary>
    /// <param name="device"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task AcceptInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject the invitation to connect from this <see cref="NearbyDevice"/>.
    /// </summary>
    /// <param name="device"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeclineInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default);
}