namespace Plugin.Maui.NearbyConnections.DeviceTests;

partial class Create
{
    /// <summary>
    /// The real platform type on iOS, wired with a real <see cref="PeerLookup"/> — the same shape
    /// <see cref="NearbyImplementation"/> constructs it with in the shipped app.
    /// </summary>
    /// <param name="options">Options to wire the platform with, or <see langword="null"/> for the suite defaults.</param>
    /// <returns>The platform under test.</returns>
    internal PlatformNearby PlatformNearby(NearbyOptions? options = null)
        => new(
            TimeProvider.System,
            options ?? DefaultOptions(),
            Logger,
            PeerLookup());

    /// <summary>A local <c>MCPeerID</c> standing in for a remote peer in a callback's arguments.</summary>
    /// <param name="displayName">The peer's display name, as MPC reports it.</param>
    /// <returns>The peer id.</returns>
    internal static MCPeerID PeerId(string displayName = "Alice") => new(displayName);

    /// <summary>
    /// The real <see cref="Plugin.Maui.NearbyConnections.PeerLookup"/> the platform is wired
    /// with. It owns peer-key derivation and handle tracking, so tests covering either resolve one
    /// of these rather than a helper type of their own.
    /// </summary>
    /// <returns>The registry.</returns>
    internal PeerLookup PeerLookup()
        => new() { Logger = Logger };

    /// <summary>
    /// Waits until <paramref name="platform"/> has registered a pending handshake. Needed after
    /// <c>AcceptAsync</c>, which registers its <c>_connectionTcs</c> entry asynchronously, so there
    /// is no signal to await — only the entry's appearance.
    /// </summary>
    /// <param name="platform">The platform to observe.</param>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    internal static async Task WaitForPendingHandshakeAsync(
        PlatformNearby platform, CancellationToken cancellationToken)
    {
        while (platform._connectionTcs.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
    }

    /// <summary>
    /// A handshake pending on the advertise channel: the platform has a registered
    /// <c>_connectionTcs</c> entry for <paramref name="peerId"/> awaiting a session-state change.
    /// </summary>
    /// <param name="platform">The platform to register the pending handshake on.</param>
    /// <param name="peerId">The remote peer the handshake is keyed by.</param>
    /// <returns>The source the platform will resolve or fault, and the peer key it is stored under.</returns>
    internal static (TaskCompletionSource<NearbyConnection> Tcs, string Id) PendingHandshake(
        PlatformNearby platform, MCPeerID peerId)
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = platform.PeerLookup.PeerKey(peerId);

        platform._connectionTcs[id] = (tcs, CancellationToken.None);

        return (tcs, id);
    }

    /// <summary>
    /// A live connection, established by driving the real platform success callback rather than by
    /// reaching into the connection's own state.
    /// </summary>
    /// <param name="platform">The platform whose callback establishes the connection.</param>
    /// <param name="peerId">The remote peer the connection is keyed by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The connection, and the platform-side peer id it is keyed by.</returns>
    internal static async Task<(NearbyConnection Connection, string Id)> ConnectedAsync(
        PlatformNearby platform,
        MCPeerID peerId,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = platform.PeerLookup.PeerKey(peerId);

        platform._connectionTcs[id] = (tcs, CancellationToken.None);
        platform.OnPeerStateChanged(peerId, MCSessionState.Connected);

        return (await tcs.Task.WaitAsync(cancellationToken), id);
    }
}
