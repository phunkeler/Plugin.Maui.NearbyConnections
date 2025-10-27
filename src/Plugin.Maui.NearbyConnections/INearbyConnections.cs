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
    /// Gets a collection of currently discovered nearby devices.
    /// </summary>
    IReadOnlyDictionary<string, INearbyDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Gets a collection of currently connected devices.
    /// </summary>
    IReadOnlyDictionary<string, INearbyDevice> ConnectedDevices { get; }

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

    /*
        /// <summary>
        /// Send data to a connected device.
        /// </summary>
        /// <param name="deviceId">Target device ID</param>
        /// <param name="data">Data to send</param>
        /// <returns>Task representing the send operation</returns>
        Task SendDataAsync(string deviceId, byte[] data);

        /// <summary>
        /// Accept a connection invitation from a device.
        /// </summary>
        /// <param name="deviceId">Device ID to accept connection from</param>
        /// <returns>Task representing the acceptance operation</returns>
        Task AcceptConnectionAsync(string deviceId);

        /// <summary>
        /// Reject a connection invitation from a device.
        /// </summary>
        /// <param name="deviceId">Device ID to reject connection from</param>
        /// <returns>Task representing the rejection operation</returns>
        Task RejectConnectionAsync(string deviceId);

    */
}