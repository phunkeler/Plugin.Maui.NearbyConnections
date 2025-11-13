using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

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
    IReadOnlyDictionary<string, INearbyDevice> Devices { get; }

    /// <summary>
    /// Gets or sets the default options to use.
    /// </summary>
    NearbyConnectionsOptions DefaultOptions { get; }

    /// <summary>
    /// Start advertising this device.
    /// </summary>
    /// <returns></returns>
    Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin discovery of <see cref="INearbyDevice"/>'s.
    /// </summary>
    /// <returns></returns>
    Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    /// <returns></returns>
    Task StopAdvertisingAsync();

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    /// <returns></returns>
    Task StopDiscoveryAsync();

    /// <summary>
    /// Sends an invitation to the specified nearby device asynchronously.
    /// </summary>
    /// <param name="device">The device to which the invitation will be sent. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the invitation operation.</param>
    /// <returns>A task that represents the asynchronous operation of sending the invitation.</returns>
    Task SendInvitation(INearbyDevice device, CancellationToken cancellationToken = default);
}