using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearbyConnections : IPlatformNearbyConnections
{
    readonly ILogger _logger;

    internal Channel<NearbyConnectionRequest> _advertiseChannel;
    internal Channel<NearbyDeviceEvent> _discoverChannel;
    internal readonly ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)> _connectionTcs;
    internal readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections;

    /// <summary>
    /// Peers already warned about payloads arriving with no <c>ReceiveAsync</c> consumer, so the
    /// warning is emitted once per connection rather than once per payload. Used as a set; the value
    /// is ignored. Entries are removed when the connection ends, so a later reconnect warns again.
    /// </summary>
    readonly ConcurrentDictionary<string, byte> _unobservedWarned = new(StringComparer.Ordinal);

    /// <summary>
    /// Tracks discovered/connected remote devices on Android, where the endpoint ID is already
    /// its own native handle so this registry uses <see cref="string"/> as the handle type.
    /// iOS uses a separate <c>RemotePeers</c> registry (see <c>NearbyConnections.shared.cs</c>'s
    /// <c>#if IOS</c> members) keyed by <c>MCPeerID</c> instead.
    /// </summary>
    internal PeerRegistry<string> Devices { get; } = new();

#if IOS
    internal PeerRegistry<MCPeerID> RemotePeers { get; init; }
    internal PeerKeyProvider PeerKeyProvider { get; init; }
    internal LocalPeerIdentityStore LocalPeerIdentityStore { get; init; }
#endif

    // Interlocked guard, not a plain bool: IPlatformNearbyConnections is a DI singleton shared by both
    // NearbyAdvertiser and NearbyDiscoverer, so container teardown can dispose it from two
    // threads at once. A non-atomic check-then-set let both callers past the guard and ran
    // PlatformDispose() twice — on iOS that double-disposes the native MCSession. Mirrors the
    // pattern already used in NearbyConnection.DisposeAsync.
    int _disposeGuard;

    internal TimeProvider TimeProvider { get; }

    public NearbyConnectionsOptions Options { get; }

    internal PlatformNearbyConnections(
        TimeProvider timeProvider,
        NearbyConnectionsOptions options,
        ILogger logger
#if IOS
        , PeerRegistry<MCPeerID> remotePeers
        , PeerKeyProvider peerKeyProvider
        , LocalPeerIdentityStore localPeerIdentityStore
#endif
        )
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        TimeProvider = timeProvider;
        Options = options;
        _logger = logger;
#if IOS
        RemotePeers = remotePeers;
        PeerKeyProvider = peerKeyProvider;
        LocalPeerIdentityStore = localPeerIdentityStore;
#endif

        _advertiseChannel = NewChannel<NearbyConnectionRequest>();
        _discoverChannel = NewChannel<NearbyDeviceEvent>();
        _connectionTcs = new ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)>(StringComparer.Ordinal);
        _activeConnections = new ConcurrentDictionary<string, NearbyConnection>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates an unbounded channel configured from <see cref="Options"/>. Every channel in the
    /// implementation — the advertise and discover streams, and each connection's receive stream —
    /// shares these settings, so they are defined once here.
    /// </summary>
    /// <param name="singleReader">
    /// <see langword="true"/> for a per-connection receive channel, which
    /// <see cref="NearbyConnection.ReceiveAsync"/> guarantees has at most one consumer. The
    /// advertise and discover streams can be re-enumerated, so they pass <see langword="false"/>.
    /// </param>
    internal Channel<T> NewChannel<T>(bool singleReader = false)
        => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = singleReader,
            SingleWriter = false,
            AllowSynchronousContinuations = Options.AllowSynchronousContinuations,
        });

    /// <inheritdoc/>
    public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var newAdvertiseChannel = NewChannel<NearbyConnectionRequest>();
        Interlocked.Exchange(ref _advertiseChannel, newAdvertiseChannel);
        await PlatformStartAdvertisingAsync(cancellationToken);

        try
        {
            await foreach (var request in newAdvertiseChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return request;
            }
        }
        finally
        {
            PlatformStopAdvertising();
            newAdvertiseChannel.Writer.TryComplete();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var newDiscoverChannel = NewChannel<NearbyDeviceEvent>();
        Interlocked.Exchange(ref _discoverChannel, newDiscoverChannel);
        await PlatformStartDiscoveringAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await foreach (var deviceEvent in newDiscoverChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return deviceEvent;
            }
        }
        finally
        {
            PlatformStopDiscovering();
            newDiscoverChannel.Writer.TryComplete();
        }
    }

    /// <inheritdoc/>
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Before anything else, including registering the TCS: an already-cancelled token must
        // surface as OperationCanceledException, not as whatever the platform happens to throw
        // first. Each platform checks internally too, but honouring it here keeps the contract
        // uniform across all three targets.
        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connectionTcs[device.Id] = (tcs, cancellationToken);

        // A plugin-owned deadline, not a platform one. iOS has a native invitation timeout, but
        // Google's Nearby Connections has none at all: requestConnection's Task completes when the
        // request is *sent*, and nothing guarantees a callback ever follows. Without this, a peer
        // that walks out of range mid-handshake leaves ConnectAsync awaiting forever and strands
        // its _connectionTcs entry. Applied on both platforms so the observable behaviour matches.
        var hasTimeout = Options.InvitationTimeout != Timeout.InfiniteTimeSpan;

        // Timed through the injected TimeProvider so this is testable with FakeTimeProvider rather
        // than requiring a real 30-second wait — the same pattern as OutgoingTransfer.
        // Timeout.InfiniteTimeSpan is a valid never-firing delay, so the infinite case needs no
        // separate construction.
        using var deadlineCts = new CancellationTokenSource(Options.InvitationTimeout, TimeProvider);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadlineCts.Token);

        try
        {
            await PlatformInitiateConnectAsync(device, timeoutCts.Token);

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (hasTimeout && !cancellationToken.IsCancellationRequested)
        {
            _connectionTcs.TryRemove(device.Id, out _);
            await PlatformAbandonConnectAsync(device);

            throw new NearbyConnectionTimeoutException(
                $"The connection request to '{device.DisplayName ?? device.Id}' was not answered within {Options.InvitationTimeout.TotalSeconds:0.#}s.");
        }
        catch
        {
            _connectionTcs.TryRemove(device.Id, out _);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => PlatformCheckAvailabilityAsync(cancellationToken);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeGuard, 1) != 0)
        {
            return;
        }

        PlatformStopAdvertising();
        PlatformStopDiscovering();

        _advertiseChannel.Writer.TryComplete();
        _discoverChannel.Writer.TryComplete();

        foreach (var (_, entry) in _connectionTcs)
        {
            entry.Tcs.TrySetCanceled(entry.Ct);
        }

        _connectionTcs.Clear();

        // Dispose every established connection before tearing down the platform. Without this,
        // each live NearbyConnection kept its receive channel open and its Disconnected task
        // unresolved, so any consumer awaiting Disconnected hung forever and the native endpoint
        // was never disconnected. Tier 2's ConnectionLifecycle.DisposeAsync already did this for
        // connections it owned; consumers using IPlatformNearbyConnections directly got no cleanup at all.
        // Snapshot first: NearbyConnection.DisposeAsync removes itself from _activeConnections.
        var connections = _activeConnections.Values.ToArray();
        _activeConnections.Clear();
        _unobservedWarned.Clear();

        foreach (var connection in connections)
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                // A single failing disconnect must not abort teardown of the rest.
                LogDisposeConnectionError(connection.RemoteDevice.Id, ex);
            }
        }

        PlatformDispose();
        Devices.Clear();
    }
}
