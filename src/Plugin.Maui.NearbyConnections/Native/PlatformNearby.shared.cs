using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby : IPlatformNearby
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
    /// This layer's own record of the remote peers it has seen — <b>not</b> the session's device
    /// set. Callbacks record a peer here so a later native callback can recover the
    /// <see cref="NearbyDevice"/> already minted for it; what a consumer observes is
    /// <c>INearby.Devices</c>, which the session maintains separately in
    /// <see cref="NearbyDeviceRegistry"/>.
    /// </summary>
    /// <remarks>
    /// Constructed by the registration code on iOS, where the registry also holds each device's
    /// native <c>MCPeerID</c> and needs a <c>PeerKeyProvider</c> to derive its keys. Android's
    /// endpoint id is already the handle, so there it needs nothing and is created here.
    /// </remarks>
    internal PeerRegistry Peers { get; }

#if IOS
    internal PeerKeyProvider PeerKeyProvider { get; init; }
    internal LocalPeerIdentityStore LocalPeerIdentityStore { get; init; }
#endif

    int _disposeGuard;

    internal TimeProvider TimeProvider { get; }

    readonly NearbyOptions _options;

    internal PlatformNearby(
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger
#if IOS
        , PeerRegistry peers
        , PeerKeyProvider peerKeyProvider
        , LocalPeerIdentityStore localPeerIdentityStore
#endif
        )
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        TimeProvider = timeProvider;
        _options = options;
        _logger = logger;
#if IOS
        ArgumentNullException.ThrowIfNull(peers);

        Peers = peers;
        PeerKeyProvider = peerKeyProvider;
        LocalPeerIdentityStore = localPeerIdentityStore;
#else
        Peers = new PeerRegistry();
#endif

        _advertiseChannel = NewChannel<NearbyConnectionRequest>();
        _discoverChannel = NewChannel<NearbyDeviceEvent>();
        _connectionTcs = new ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)>(StringComparer.Ordinal);
        _activeConnections = new ConcurrentDictionary<string, NearbyConnection>(StringComparer.Ordinal);
    }

    internal Channel<T> NewChannel<T>(bool singleReader = false)
        => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = singleReader,
            SingleWriter = false,
            AllowSynchronousContinuations = _options.AllowSynchronousContinuations,
        });

    /// <inheritdoc/>
    public async IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        TaskCompletionSource started,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var newAdvertiseChannel = NewChannel<NearbyConnectionRequest>();
        Interlocked.Exchange(ref _advertiseChannel, newAdvertiseChannel);

        try
        {
            await PlatformStartAdvertisingAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            PlatformStopAdvertising();
            newAdvertiseChannel.Writer.TryComplete();
            started.TrySetException(ex);
            throw;
        }

        // A late fault (the channel faults after this point, e.g. the radio drops mid-session) must
        // not retroactively fault an already-resolved started — TrySetResult, not SetResult.
        started.TrySetResult();

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
        TaskCompletionSource started,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var newDiscoverChannel = NewChannel<NearbyDeviceEvent>();
        Interlocked.Exchange(ref _discoverChannel, newDiscoverChannel);

        try
        {
            await PlatformStartDiscoveryAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            PlatformStopDiscovering();
            newDiscoverChannel.Writer.TryComplete();
            started.TrySetException(ex);
            throw;
        }

        started.TrySetResult();

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

        var tcs = RegisterConnectionTcs(device.Id, cancellationToken);

        // A plugin-owned deadline, not a platform one. iOS has a native invitation timeout, but
        // Google's Nearby Connections has none at all: requestConnection's Task completes when the
        // request is *sent*, and nothing guarantees a callback ever follows. Without this, a peer
        // that walks out of range mid-handshake leaves ConnectAsync awaiting forever and strands
        // its _connectionTcs entry. Applied on both platforms so the observable behaviour matches.
        var hasTimeout = _options.InvitationTimeout != Timeout.InfiniteTimeSpan;

        // Timed through the injected TimeProvider so this is testable with FakeTimeProvider rather
        // than requiring a real 30-second wait — the same pattern as OutgoingTransfer.
        // Timeout.InfiniteTimeSpan is a valid never-firing delay, so the infinite case needs no
        // separate construction.
        using var deadlineCts = new CancellationTokenSource(_options.InvitationTimeout, TimeProvider);

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
                $"The connection request to '{device.DisplayName ?? device.Id}' was not answered within {_options.InvitationTimeout.TotalSeconds:0.#}s.");
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

    /// <summary>
    /// Builds the exception both platforms throw when a file transfer's inactivity timeout fires,
    /// and logs it first. Centralised so the message and the log call can't drift between the
    /// Android and iOS <c>PlatformSendFileAsync</c> catch clauses — each platform still reports its
    /// own terminal progress before calling this, since the progress mechanics genuinely differ.
    /// </summary>
    NearbyTransferTimeoutException TransferInactivityTimeoutException(string deviceId)
    {
        LogSendFileTimeout(deviceId, null, _options.TransferInactivityTimeout.TotalSeconds);

        return new NearbyTransferTimeoutException(
            $"Transfer stalled: no progress received for {_options.TransferInactivityTimeout}.");
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
        // connections it owned; consumers using IPlatformNearby directly got no cleanup at all.
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
        Peers.Clear();
    }
}
