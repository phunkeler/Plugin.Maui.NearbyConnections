using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby : IPlatformNearby
{
    readonly ILogger _logger;
    readonly NearbyOptions _options;
    readonly ConcurrentDictionary<string, byte> _unobservedWarned = new(StringComparer.Ordinal);

    internal readonly ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)> _connectionTcs;
    internal readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections;

    int _disposed;

    internal Channel<NearbyConnectionRequest> _advertiseChannel;
    internal Channel<NearbyDeviceEvent> _discoverChannel;

    internal PeerLookup PeerLookup { get; }
    internal TimeProvider TimeProvider { get; }

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
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDisposeConnectionError(connection.RemoteDevice.Id, ex);
            }
        }

        await PlatformDrainPayloadCompletionAsync().ConfigureAwait(false);

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
            _connectionTcs.TryRemove(device.Id, out _);
            throw;
        }
    }

    NearbyTransferTimeoutException TransferInactivityTimeoutException(string deviceId)
    {
        LogSendFileTimeout(deviceId, null, _options.TransferInactivityTimeout.TotalSeconds);

        return new NearbyTransferTimeoutException(
            $"Transfer stalled: no progress received for {_options.TransferInactivityTimeout}.");
    }

    void ReleaseConnectionFromCallback(string peerId)
    {
        var release = ReleaseConnectionAsync(peerId);

        if (release.IsCompletedSuccessfully)
        {
            return;
        }

        _ = Await(release, peerId);

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