using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby : IPlatformNearby
{
    /// <summary>
    /// How long a drain waits before it gives up and lets the release proceed. A constant rather
    /// than a <see cref="NearbyOptions"/> value: the bound exists so that disposal terminates, and
    /// no consumer scenario wants a different value.
    /// </summary>
    static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    readonly ILogger _logger;
    readonly NearbyOptions _options;
    readonly ConcurrentDictionary<string, byte> _unobservedWarned = new(StringComparer.Ordinal);

    /// <summary>
    /// Orders the per-peer work that platform callbacks start and cannot await, so that a release
    /// or a disposal can wait for that work before it frees the handles the work reads.
    /// </summary>
    readonly KeyedSerialQueue _workQueue;

    internal readonly ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)> _connectionTcs;
    internal readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections;

    int _disposed;

    internal Channel<NearbyConnectionRequest> _advertiseChannel;
    internal Channel<NearbyDeviceEvent> _discoverChannel;

    internal PeerLookup PeerLookup { get; }
    internal TimeProvider TimeProvider { get; }

    /// <summary>
    /// The platform adapter, created by <see cref="PlatformCreateAdapter"/> during construction.
    /// Null only on <c>net10.0</c>, whose <c>Platform*</c> stubs never reach it.
    /// </summary>
    /// <remarks>
    /// Typed as the interface on purpose: the seam is the design (decision D5), and each TFM
    /// assigning exactly one implementation is what CA1859 sees — not a mistake to optimize away.
    /// CS0649 is the <c>net10.0</c> target, which never assigns the field: its throwing stubs
    /// answer before any call could reach an adapter.
    /// </remarks>
#pragma warning disable CA1859, CS0649
    IPlatformAdapter? _adapter;
#pragma warning restore CA1859, CS0649

    /// <summary>The session's options snapshot, for the adapter's SDK calls.</summary>
    internal NearbyOptions Options => _options;

    /// <summary>The session's logger, for the adapter's own log messages.</summary>
    internal ILogger Logger => _logger;

    /// <summary>The per-peer work queue, for callback work the adapter cannot await (C6).</summary>
    internal KeyedSerialQueue WorkQueue => _workQueue;

    /// <summary>The advertise channel's completion — the iOS start-failure grace window awaits it.</summary>
    internal Task AdvertiseChannelCompletion => _advertiseChannel.Reader.Completion;

    /// <summary>The discover channel's completion. See <see cref="AdvertiseChannelCompletion"/>.</summary>
    internal Task DiscoverChannelCompletion => _discoverChannel.Reader.Completion;

    /// <summary>Creates this platform's <see cref="IPlatformAdapter"/>. No <c>net10.0</c> implementation.</summary>
    partial void PlatformCreateAdapter();

    internal PlatformNearby(
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger,
        PeerLookup peerLookup)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(peerLookup);

        TimeProvider = timeProvider;
        _options = options;
        _logger = logger;
        PeerLookup = peerLookup;

        _advertiseChannel = NewChannel<NearbyConnectionRequest>();
        _discoverChannel = NewChannel<NearbyDeviceEvent>();
        _connectionTcs = new ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)>(StringComparer.Ordinal);
        _activeConnections = new ConcurrentDictionary<string, NearbyConnection>(StringComparer.Ordinal);
        _workQueue = new KeyedSerialQueue(
            (key, ex) => LogCallbackError(nameof(KeyedSerialQueue), key, ex));

        PlatformCreateAdapter();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        TaskCompletionSource started,
        CancellationToken cancellationToken = default)
        => Step(
            () => NewChannel<NearbyConnectionRequest>(),
            channel => Interlocked.Exchange(ref _advertiseChannel, channel),
            PlatformStartAdvertisingAsync,
            PlatformStopAdvertising,
            started,
            cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<NearbyDeviceEvent> DiscoverAsync(
        TaskCompletionSource started,
        CancellationToken cancellationToken = default)
        => Step(
            () => NewChannel<NearbyDeviceEvent>(),
            channel => Interlocked.Exchange(ref _discoverChannel, channel),
            PlatformStartDiscoveryAsync,
            PlatformStopDiscovering,
            started,
            cancellationToken);

    /// <inheritdoc/>
    public async Task<NearbyConnection> ConnectAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        cancellationToken.ThrowIfCancellationRequested();

        var tcs = RegisterConnectionTcs(device.Id, cancellationToken);

        return await AwaitHandshakeAsync(
            device,
            tcs,
            ConnectionRole.Initiator,
            beforeAwait: token => PlatformInitiateConnectAsync(device, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => PlatformCheckAvailabilityAsync(cancellationToken);

    /// <inheritdoc/>
    public bool TryGetConnection(string deviceId, [NotNullWhen(true)] out NearbyConnection? connection)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        return _activeConnections.TryGetValue(deviceId, out connection);
    }

    /// <inheritdoc/>
    public NearbyConnection[] SnapshotConnections() => [.. _activeConnections.Values];

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
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

        var connections = _activeConnections.Values.ToArray();
        _activeConnections.Clear();
        _unobservedWarned.Clear();

        foreach (var connection in connections)
        {
            try
            {
                connection.DisposeReason = NearbyEndReason.SessionStopped;
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposeConnectionError(connection.RemoteDevice.Id, ex);
            }
        }

        if (!await _workQueue.DrainAllAsync(DrainTimeout).ConfigureAwait(false))
        {
            LogPayloadDrainTimedOut(_workQueue.KeyCount, DrainTimeout.TotalSeconds);
        }

        PlatformDispose();
        PeerLookup.Clear();
        PlatformSweepStaging();
    }

    internal async Task<NearbyConnection> AwaitHandshakeAsync(
        NearbyDevice device,
        TaskCompletionSource<NearbyConnection> tcs,
        ConnectionRole role,
        Func<CancellationToken, Task> beforeAwait,
        CancellationToken cancellationToken)
    {
        var isInitiator = role is ConnectionRole.Initiator;
        var timeout = isInitiator ? _options.ConnectTimeout : _options.AcceptTimeout;
        var hasTimeout = timeout != Timeout.InfiniteTimeSpan;

        using var deadlineCts = new CancellationTokenSource(timeout, TimeProvider);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadlineCts.Token);

        try
        {
            await beforeAwait(timeoutCts.Token).ConfigureAwait(false);

            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (hasTimeout
                && deadlineCts.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)
        {
            _connectionTcs.TryRemove(device.Id, out _);
            await PlatformAbandonConnectAsync(device).ConfigureAwait(false);

            var name = device.DisplayName ?? device.Id;
            var seconds = timeout.TotalSeconds;

            throw new NearbyConnectionTimeoutException(isInitiator
                ? $"The connection request to '{name}' was not answered within {seconds:0.#}s."
                : $"The connection with '{name}' was not established within {seconds:0.#}s of accepting the request.");
        }
        catch
        {
            // Same abandon-and-release the deadline exit runs: a handshake that exits through a
            // cancelled caller or a faulted platform call must not leave the platform holding a
            // half-open connection nothing will ever finish.
            _connectionTcs.TryRemove(device.Id, out _);

            try
            {
                await PlatformAbandonConnectAsync(device).ConfigureAwait(false);
            }
            catch (Exception abandonEx)
            {
                LogWriteError(nameof(PlatformAbandonConnectAsync), device.Id, abandonEx);
            }

            throw;
        }
    }

    internal NearbyTransferTimeoutException TransferInactivityTimeoutException(string deviceId)
    {
        LogSendFileTimeout(deviceId, null, _options.TransferInactivityTimeout.TotalSeconds);

        return new NearbyTransferTimeoutException(
            $"Transfer stalled: no progress received for {_options.TransferInactivityTimeout}.");
    }

    internal void ReleaseConnectionFromCallback(string deviceId)
    {
        var release = ReleaseConnectionAsync(deviceId);

        if (release.IsCompletedSuccessfully)
        {
            return;
        }

        _ = Await(release, deviceId);

        async Task Await(ValueTask pending, string id)
        {
            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWriteError(nameof(ReleaseConnectionFromCallback), id, ex);
            }
        }
    }

    static async IAsyncEnumerable<T> Step<T>(
        Func<Channel<T>> createChannel,
        Action<Channel<T>> publish,
        Func<CancellationToken, Task> start,
        Action stop,
        TaskCompletionSource started,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = createChannel();
        publish(channel);

        try
        {
            await start(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stop();
            channel.Writer.TryComplete();
            started.TrySetException(ex);
            throw;
        }

        started.TrySetResult();

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            stop();
            channel.Writer.TryComplete();
        }
    }

    internal static Channel<T> NewChannel<T>(bool singleReader = false)
        => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = singleReader,
            SingleWriter = false,
        });
}