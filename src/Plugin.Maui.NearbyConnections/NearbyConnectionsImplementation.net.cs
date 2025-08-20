#if !IOS && !ANDROID
using Plugin.Maui.NearbyConnections.Models;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// .NET (non-platform specific) implementation that provides stub methods.
/// </summary>
public partial class NearbyConnectionsImplementation
{
    /// <summary>
    /// Platform-specific initialization logic (no-op for .NET).
    /// </summary>
    partial void InitializePlatform()
    {
        // No platform-specific initialization needed for .NET
    }

    /// <summary>
    /// Platform-specific disposal logic (no-op for .NET).
    /// </summary>
    partial void DisposePlatform()
    {
        // No platform-specific disposal needed for .NET
    }

    private partial Task ConnectToPeerAsyncImpl(string peerId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Peer connection functionality not yet implemented for this platform.");
    }

    private partial Task DisconnectFromPeerAsyncImpl(string peerId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Peer disconnection functionality not yet implemented for this platform.");
    }

    private partial Task SendDataAsyncImpl(byte[] data, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Message sending functionality not yet implemented for this platform.");
    }

    private partial IReadOnlyList<PeerDevice> GetConnectedPeersImpl()
    {
        return new List<PeerDevice>();
    }

    private partial IReadOnlyList<PeerDevice> GetDiscoveredPeersImpl()
    {
        return new List<PeerDevice>();
    }
}
#endif