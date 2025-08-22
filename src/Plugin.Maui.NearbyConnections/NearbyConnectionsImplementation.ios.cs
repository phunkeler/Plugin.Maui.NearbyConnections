#if IOS
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;
using Plugin.Maui.NearbyConnections.Session;

namespace Plugin.Maui.NearbyConnections;

// All things that need to start once, when MAUI does, should go in a DI-registered implementation of IMauiInitializer
//

/// <summary>
/// iOS-specific implementation that adds session management and messaging support.
/// </summary>
public partial class NearbyConnectionsImplementation
{
    private SessionManager? _sessionManager;
    private NearbyConnectionsManager? _nearbyManager;

    /// <summary>
    /// Initializes iOS-specific components.
    /// </summary>
    partial void InitializePlatform()
    {
        _nearbyManager = new NearbyConnectionsManager();
    }

    /// <summary>
    /// Disposes iOS-specific components.
    /// </summary>
    partial void DisposePlatform()
    {
        if (_sessionManager is not null)
        {
            _sessionManager.PeerDiscovered -= OnPeerDiscovered;
            _sessionManager.PeerConnectionChanged -= OnPeerConnectionChanged;
            _sessionManager.MessageReceived -= OnMessageReceived;
            _sessionManager.Dispose();
            _sessionManager = null;
        }

        _nearbyManager?.Dispose();
        _nearbyManager = null;
    }

    private partial Task ConnectToPeerAsyncImpl(string peerId, CancellationToken cancellationToken)
    {
        EnsureSessionManager();
        return _sessionManager!.ConnectToPeerAsync(peerId, cancellationToken);
    }

    private partial Task DisconnectFromPeerAsyncImpl(string peerId, CancellationToken cancellationToken)
    {
        EnsureSessionManager();
        return _sessionManager!.DisconnectFromPeerAsync(peerId, cancellationToken);
    }

    private partial Task SendDataAsyncImpl(byte[] data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureSessionManager();
        return _sessionManager!.SendDataAsync(data, cancellationToken);
    }

    private partial IReadOnlyList<PeerDevice> GetConnectedPeersImpl()
    {
        EnsureSessionManager();
        return _sessionManager!.GetConnectedPeers();
    }

    private partial IReadOnlyList<PeerDevice> GetDiscoveredPeersImpl()
    {
        EnsureSessionManager();
        return _sessionManager!.GetDiscoveredPeers();
    }

    private void EnsureSessionManager()
    {
        if (_sessionManager is null)
        {
            if (_nearbyManager is null)
                throw new InvalidOperationException("Nearby manager is not initialized.");

            // Create a peer ID for this device - in a real app, you might want to get this from user settings
            var displayName = Environment.UserName ?? Environment.MachineName ?? "Unknown Device";
            var peerId = _nearbyManager.GetPeerId(displayName);

            if (peerId == null)
                throw new InvalidOperationException("Failed to create or retrieve peer ID.");

            _sessionManager = new SessionManager(peerId);

            // Forward events from SessionManager to our public events
            _sessionManager.PeerDiscovered += OnPeerDiscovered;
            _sessionManager.PeerConnectionChanged += OnPeerConnectionChanged;
            _sessionManager.MessageReceived += OnMessageReceived;
        }
    }
}
#endif