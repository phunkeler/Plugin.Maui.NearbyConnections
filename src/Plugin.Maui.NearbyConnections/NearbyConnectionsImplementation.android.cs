#if ANDROID
using Plugin.Maui.NearbyConnections.Models;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Android-specific implementation that provides stub methods for now.
/// </summary>
public partial class NearbyConnectionsImplementation
{
    /// <summary>
    /// Platform-specific initialization logic for Android.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    partial void InitializePlatform()
#pragma warning restore CA1822 // Mark members as static
    {

    }

    /// <summary>
    /// Platform-specific disposal logic for Android.
    /// </summary>
    partial void DisposePlatform()
    {
        throw new NotImplementedException("Android Nearby Connections implementation not yet available.");
    }

    private partial Task ConnectToPeerAsyncImpl(string peerId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Android Nearby Connections implementation not yet available.");
    }

    private partial Task DisconnectFromPeerAsyncImpl(string peerId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Android Nearby Connections implementation not yet available.");
    }

    private partial Task SendDataAsyncImpl(byte[] data, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Android Nearby Connections implementation not yet available.");
    }

    private partial IReadOnlyList<PeerDevice> GetConnectedPeersImpl()
    {
        throw new NotImplementedException("Android Nearby Connections implementation not yet available.");
    }

    private partial IReadOnlyList<PeerDevice> GetDiscoveredPeersImpl()
    {
        throw new NotImplementedException("Android Nearby Connections implementation not yet available.");
    }
}
#endif