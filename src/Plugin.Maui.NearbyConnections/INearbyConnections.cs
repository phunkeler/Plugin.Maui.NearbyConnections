using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;
using AdvertisingOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides access to the Nearby Connections plugin functionality.
/// </summary>
public interface INearbyConnections
{
    /// <summary>
    /// Fired when the advertising state changes.
    /// This event remains active across advertiser dispose/recreate cycles.
    /// </summary>
    event EventHandler<AdvertisingStateChangedEventArgs> AdvertisingStateChanged;

    /// <summary>
    /// Fired when the discovering state changes.
    /// This event remains active across discoverer dispose/recreate cycles.
    /// </summary>
    event EventHandler<DiscoveringStateChangedEventArgs> DiscoveringStateChanged;

    /// <summary>
    /// Fired when a peer is discovered during the discovery process.
    /// </summary>
    event EventHandler<PeerDiscoveredEventArgs> PeerDiscovered;

    /// <summary>
    /// Fired when a peer's connection state changes.
    /// </summary>
    event EventHandler<PeerConnectionChangedEventArgs> PeerConnectionChanged;

    /// <summary>
    /// Fired when a message is received from a connected peer.
    /// </summary>
    event EventHandler<PeerMessageReceivedEventArgs> MessageReceived;

    /// <summary>
    /// Starts advertising for nearby devices.
    /// </summary>
    /// <param name="options">
    /// The <see cref="AdvertisingOptions"/> to use for configuring advertising behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops advertising for nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StopAdvertisingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts discovering for nearby devices.
    /// </summary>
    /// <param name="options">
    /// The <see cref="DiscoveringOptions"/> to use for configuring discovery behavior.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StartDiscoveryAsync(DiscoveringOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops discovering for nearby devices.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken" /> to cancel the operation.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    Task StopDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to a discovered peer device.
    /// </summary>
    /// <param name="peerId">The ID of the peer to connect to.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ConnectToPeerAsync(string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from a connected peer device.
    /// </summary>
    /// <param name="peerId">The ID of the peer to disconnect from.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisconnectFromPeerAsync(string peerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to all connected peers.
    /// </summary>
    /// <param name="message">The message to send as a UTF-8 string.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to all connected peers.
    /// </summary>
    /// <param name="data">The data to send as a byte array.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a data payload to all connected peers.
    /// </summary>
    /// <param name="payload">The data payload to send (bytes, file, or stream).</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendDataAsync(DataPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a file to all connected peers.
    /// </summary>
    /// <param name="filePath">The path to the file to send.</param>
    /// <param name="fileName">Optional custom name for the file. If not provided, uses the actual filename.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendFileAsync(string filePath, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends stream data to all connected peers.
    /// </summary>
    /// <param name="stream">The stream to send data from.</param>
    /// <param name="streamName">A name to identify the stream.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendStreamAsync(Stream stream, string streamName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of currently connected peers.
    /// </summary>
    /// <returns>A collection of connected peer devices.</returns>
    IReadOnlyList<PeerDevice> GetConnectedPeers();

    /// <summary>
    /// Gets the list of discovered peers.
    /// </summary>
    /// <returns>A collection of discovered peer devices.</returns>
    IReadOnlyList<PeerDevice> GetDiscoveredPeers();
}