using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly ILogger _logger;

    internal Channel<NearbyConnectionRequest> _advertiseChannel;
    internal Channel<NearbyDeviceEvent> _discoverChannel;
    internal readonly ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)> _connectionTcs;
    internal readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections;

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

    // Interlocked guard, not a plain bool: INearbyConnections is a DI singleton shared by both
    // NearbyAdvertiser and NearbyDiscoverer, so container teardown can dispose it from two
    // threads at once. A non-atomic check-then-set let both callers past the guard and ran
    // PlatformDispose() twice — on iOS that double-disposes the native MCSession. Mirrors the
    // pattern already used in NearbyConnection.DisposeAsync.
    int _disposeGuard;

    internal TimeProvider TimeProvider { get; }

    public NearbyConnectionsOptions Options { get; }

    internal NearbyConnectionsImplementation(
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

        var channelOptions = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = options.AllowSynchronousContinuations,
        };

        _advertiseChannel = Channel.CreateUnbounded<NearbyConnectionRequest>(channelOptions);
        _discoverChannel = Channel.CreateUnbounded<NearbyDeviceEvent>(channelOptions);
        _connectionTcs = new ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)>(StringComparer.Ordinal);
        _activeConnections = new ConcurrentDictionary<string, NearbyConnection>(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var newAdvertiseChannel = Channel.CreateUnbounded<NearbyConnectionRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = Options.AllowSynchronousContinuations,
            });
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
        var newDiscoverChannel = Channel.CreateUnbounded<NearbyDeviceEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = Options.AllowSynchronousContinuations,
            });
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

        var tcs = new TaskCompletionSource<NearbyConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connectionTcs[device.Id] = (tcs, cancellationToken);

        await PlatformInitiateConnectAsync(device, cancellationToken);

        try
        {
            return await tcs.Task.WaitAsync(cancellationToken);
        }
        catch
        {
            _connectionTcs.TryRemove(device.Id, out _);
            throw;
        }
    }

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
        // connections it owned; consumers using INearbyConnections directly got no cleanup at all.
        // Snapshot first: NearbyConnection.DisposeAsync removes itself from _activeConnections.
        var connections = _activeConnections.Values.ToArray();
        _activeConnections.Clear();

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
