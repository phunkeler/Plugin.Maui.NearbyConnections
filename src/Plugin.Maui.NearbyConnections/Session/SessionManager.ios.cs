#if IOS
using Foundation;
using MultipeerConnectivity;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;
using System.Collections.Concurrent;

namespace Plugin.Maui.NearbyConnections.Session;

/// <summary>
/// iOS-specific session manager that handles MultipeerConnectivity sessions.
/// </summary>
public class SessionManager : NSObject, IMCSessionDelegate, IDisposable
{
    readonly ConcurrentDictionary<string, PeerDevice> _discoveredPeers = [];
    readonly ConcurrentDictionary<string, PeerDevice> _connectedPeers = [];
    readonly MCPeerID _localPeerId;
    MCSession? _session;
    bool _disposed;

    /// <summary>
    /// Fired when a peer is discovered.
    /// </summary>
    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;

    /// <summary>
    /// Fired when a peer's connection state changes.
    /// </summary>
    public event EventHandler<PeerConnectionChangedEventArgs>? PeerConnectionChanged;

    /// <summary>
    /// Fired when a message is received from a peer.
    /// </summary>
    public event EventHandler<PeerMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Initializes a new instance of the SessionManager.
    /// </summary>
    /// <param name="localPeerId">The local peer ID for this device.</param>
    public SessionManager(MCPeerID localPeerId)
    {
        _localPeerId = localPeerId ?? throw new ArgumentNullException(nameof(localPeerId));
        _session = new MCSession(_localPeerId);
        _session.Delegate = this;
    }

    /// <summary>
    /// Adds a discovered peer to the list.
    /// </summary>
    /// <param name="peerId">The discovered peer ID.</param>
    /// <param name="context">Optional context data.</param>
    public void AddDiscoveredPeer(MCPeerID peerId, NSData? context = null)
    {
        var peer = new PeerDevice
        {
            Id = peerId.DisplayName,
            DisplayName = peerId.DisplayName,
            Context = context?.ToString(),
            ConnectionState = PeerConnectionState.NotConnected
        };

        if (_discoveredPeers.TryAdd(peer.Id, peer))
        {
            PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs { Peer = peer });
        }
    }

    /// <summary>
    /// Connects to a discovered peer.
    /// </summary>
    /// <param name="peerId">The ID of the peer to connect to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ConnectToPeerAsync(string peerId, CancellationToken cancellationToken = default)
    {
        if (_session == null || _disposed)
            throw new InvalidOperationException("Session is not available.");

        if (!_discoveredPeers.TryGetValue(peerId, out var peer))
            throw new ArgumentException($"Peer {peerId} not found in discovered peers.", nameof(peerId));

        // Update peer state to connecting
        UpdatePeerConnectionState(peer, PeerConnectionState.Connecting);

        // Note: In a real implementation, peer connection would be handled by MCNearbyServiceBrowser
        // and the browser's invitation process. For now, we'll simulate a connection.
        // This is a simplified implementation for demonstration purposes.

        return Task.CompletedTask;
    }

    /// <summary>
    /// Disconnects from a connected peer.
    /// </summary>
    /// <param name="peerId">The ID of the peer to disconnect from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DisconnectFromPeerAsync(string peerId, CancellationToken cancellationToken = default)
    {
        if (_session == null || _disposed)
            throw new InvalidOperationException("Session is not available.");

        if (_connectedPeers.TryGetValue(peerId, out var peer))
        {
            UpdatePeerConnectionState(peer, PeerConnectionState.Disconnecting);
            _session.Disconnect();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends data to all connected peers.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_session == null || _disposed)
            throw new InvalidOperationException("Session is not available.");

        if (_session.ConnectedPeers.Length == 0)
            throw new InvalidOperationException("No connected peers to send data to.");

        try
        {
            var nsData = NSData.FromArray(data);
            var success = _session.SendData(nsData, _session.ConnectedPeers, MCSessionSendDataMode.Reliable, out var error);

            if (!success && error != null)
            {
                throw new InvalidOperationException($"Failed to send data: {error.LocalizedDescription}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to send data: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a resource to all connected peers.
    /// </summary>
    /// <param name="resourceUrl">A file or HTTP URL.</param>
    /// <param name="cancellationToken">A name for the resource.</param>
    /// <param name="resourceName">The peer that should receive this resource.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task SendResourceAsync(
        NSUrl resourceUrl,
        string? resourceName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUrl);

        if (_session is null || _disposed)
            throw new InvalidOperationException("Session is not available.");

        if (_session.ConnectedPeers.Length == 0)
            throw new InvalidOperationException("No connected peers to send resource to.");

        resourceName ??= resourceUrl.LastPathComponent;

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            resourceName = resourceUrl.AbsoluteString;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var sendTasks = new List<Task>();

        foreach (var peer in _session.ConnectedPeers)
        {
            if (peer is null)
                continue;

            cancellationToken.ThrowIfCancellationRequested();

            var sendTask = Task.Run(async () =>
            {
                try
                {
                    await _session.SendResourceAsync(resourceUrl, resourceName!, peer, out var progress);

                    // Optionally monitor progress with cancellation
                    while (progress is not null && !progress.Finished && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Resource send to {peer.DisplayName} was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send resource to {peer.DisplayName}: {ex.Message}");
                }
            }, cancellationToken);

            sendTasks.Add(sendTask);
        }

        await Task.WhenAll(sendTasks);
    }

    /// <summary>
    /// Gets the list of connected peers.
    /// </summary>
    public IReadOnlyList<PeerDevice> GetConnectedPeers() => _connectedPeers.Values.ToList();

    /// <summary>
    /// Gets the list of discovered peers.
    /// </summary>
    public IReadOnlyList<PeerDevice> GetDiscoveredPeers() => _discoveredPeers.Values.ToList();

    private void UpdatePeerConnectionState(PeerDevice peer, PeerConnectionState newState)
    {
        var previousState = peer.ConnectionState;
        peer.ConnectionState = newState;

        // Update collections based on new state
        switch (newState)
        {
            case PeerConnectionState.Connected:
                _connectedPeers.TryAdd(peer.Id, peer);
                break;
            case PeerConnectionState.NotConnected:
                _connectedPeers.TryRemove(peer.Id, out _);
                break;
        }

        PeerConnectionChanged?.Invoke(this, new PeerConnectionChangedEventArgs
        {
            Peer = peer,
            PreviousState = previousState
        });
    }

    #region IMCSessionDelegate

    /// <inheritdoc/>
    public void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
    {
        var peerId = peerID.DisplayName;

        // Find or create peer device
        if (!_discoveredPeers.TryGetValue(peerId, out var peer))
        {
            peer = new PeerDevice
            {
                Id = peerId,
                DisplayName = peerId,
                ConnectionState = PeerConnectionState.NotConnected
            };
            _discoveredPeers.TryAdd(peerId, peer);
        }

        var newConnectionState = state switch
        {
            MCSessionState.Connecting => PeerConnectionState.Connecting,
            MCSessionState.Connected => PeerConnectionState.Connected,
            MCSessionState.NotConnected => PeerConnectionState.NotConnected,
            _ => PeerConnectionState.NotConnected
        };

        UpdatePeerConnectionState(peer, newConnectionState);
    }

    /// <inheritdoc/>
    public void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
    {
        var messageData = new MessageData
        {
            FromPeerId = peerID.DisplayName,
            FromDisplayName = peerID.DisplayName,
            Data = data.ToArray(),
            Timestamp = DateTimeOffset.UtcNow
        };

        MessageReceived?.Invoke(this, new PeerMessageReceivedEventArgs { Message = messageData });
    }

    /// <inheritdoc/>
    public void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
    {

    }

    /// <inheritdoc/>
    public void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl? localUrl, NSError? error)
    {

    }

    /// <inheritdoc/>
    public void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID)
    {

    }

    #endregion

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _session?.Disconnect();
                _session?.Dispose();
                _session = null;
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}
#endif