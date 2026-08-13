using Microsoft.Extensions.Logging.Abstractions;

namespace Plugin.Maui.NearbyConnections.DeviceTests;

static partial class Create
{
    /// <summary>
    /// The real platform type on iOS, wired with real <see cref="PeerRegistry"/>,
    /// <see cref="PeerKeyProvider"/>, and <see cref="LocalPeerIdentityStore"/> instances — the same
    /// shape <see cref="NearbyImplementation"/> constructs it with in the shipped app.
    /// </summary>
    /// <param name="options">Options to wire the platform with, or <see langword="null"/> for the suite defaults.</param>
    /// <returns>The platform under test.</returns>
    public static PlatformNearby PlatformNearby(NearbyOptions? options = null)
    {
        var peerKeyProvider = PeerKeyProvider();

        return new PlatformNearby(
            TimeProvider.System,
            options ?? DefaultOptions(),
            NullLogger.Instance,
            new PeerRegistry { PeerKeyProvider = peerKeyProvider, Logger = NullLogger.Instance },
            peerKeyProvider,
            LocalPeerIdentityStore());
    }

    /// <summary>A local <c>MCPeerID</c> standing in for a remote peer in a callback's arguments.</summary>
    /// <param name="displayName">The peer's display name, as MPC reports it.</param>
    /// <returns>The peer id.</returns>
    public static MCPeerID PeerId(string displayName = "Alice") => new(displayName);

    /// <summary>The real <see cref="PeerKeyProvider"/> the platform is wired with.</summary>
    /// <returns>The provider.</returns>
    public static PeerKeyProvider PeerKeyProvider() => new(NullLogger<PeerKeyProvider>.Instance);

    /// <summary>The real <see cref="LocalPeerIdentityStore"/> the platform is wired with.</summary>
    /// <returns>The store.</returns>
    public static LocalPeerIdentityStore LocalPeerIdentityStore() => new(NullLogger<LocalPeerIdentityStore>.Instance);

    /// <summary>
    /// A live connection, established by driving the real platform success callback rather than by
    /// reaching into the connection's own state.
    /// </summary>
    /// <param name="platform">The platform whose callback establishes the connection.</param>
    /// <param name="peerId">The remote peer the connection is keyed by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The connection, and the platform-side peer id it is keyed by.</returns>
    public static async Task<(NearbyConnection Connection, string Id)> ConnectedAsync(
        PlatformNearby platform,
        MCPeerID peerId,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = platform.Peers.PeerKeyProvider.PeerKey(peerId);

        platform._connectionTcs[id] = (tcs, CancellationToken.None);
        platform.OnPeerStateChanged(peerId, MCSessionState.Connected);

        return (await tcs.Task.WaitAsync(cancellationToken), id);
    }
}
