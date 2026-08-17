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

    readonly ConcurrentDictionary<string, byte> _unobservedWarned = new(StringComparer.Ordinal);

    internal PeerRegistry Peers { get; }

    int _disposeGuard;

    internal TimeProvider TimeProvider { get; }

    readonly NearbyOptions _options;

    internal PlatformNearby(
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger,
        PeerRegistry peers)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(peers);

        TimeProvider = timeProvider;
        _options = options;
        _logger = logger;
        Peers = peers;

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
    public IAsyncEnumerable<NearbyConnectionRequest> AdvertiseAsync(
        TaskCompletionSource started,
        CancellationToken cancellationToken = default)
        => StreamAsync(
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
        => StreamAsync(
            () => NewChannel<NearbyDeviceEvent>(),
            channel => Interlocked.Exchange(ref _discoverChannel, channel),
            PlatformStartDiscoveryAsync,
            PlatformStopDiscovering,
            started,
            cancellationToken);

    /// <summary>
    /// Publishes a fresh channel as the live one, starts the platform, resolves
    /// <paramref name="started"/>, then yields everything written to that channel until the
    /// enumeration ends. Stops the platform and completes the channel on every exit path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by <see cref="AdvertiseAsync"/> and <see cref="DiscoverAsync"/>, which differ only in
    /// element type and in which pair of platform hooks they drive. The two bodies were otherwise
    /// identical, and this is the same reasoning that keeps <see cref="AwaitHandshakeAsync"/> shared:
    /// a fix applied to one and not its sibling is this codebase's dominant defect class.
    /// </para>
    /// <para>
    /// <paramref name="publish"/> is a delegate rather than a <c>ref</c> parameter to the channel
    /// field, because C# forbids <c>ref</c> parameters in an iterator. Channel construction stays
    /// inside the iterator body so that nothing happens until enumeration begins, which is the
    /// contract <see cref="IPlatformNearby.AdvertiseAsync"/> documents — building the enumerable and
    /// never enumerating it must not swap the live channel.
    /// </para>
    /// </remarks>
    static async IAsyncEnumerable<T> StreamAsync<T>(
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
            await start(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stop();
            channel.Writer.TryComplete();
            started.TrySetException(ex);
            throw;
        }

        // A late fault (the channel faults after this point, e.g. the radio drops mid-session) must
        // not retroactively fault an already-resolved started — TrySetResult, not SetResult.
        started.TrySetResult();

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
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
            cancellationToken);
    }

    /// <summary>
    /// Awaits a registered handshake under the plugin's own deadline, converting an elapsed deadline
    /// into <see cref="NearbyConnectionTimeoutException"/> and removing the
    /// <c>_connectionTcs</c> entry on every exit path.
    /// </summary>
    /// <param name="device">The remote device the handshake is with.</param>
    /// <param name="tcs">
    /// The already-registered source the platform's terminal callback resolves or faults. Registered
    /// by the caller rather than here, because both platforms must register before the step that can
    /// make a callback fire.
    /// </param>
    /// <param name="role">
    /// Which side of the handshake this caller is on. Selects both the deadline and the message:
    /// <see cref="NearbyOptions.ConnectTimeout"/> for an initiator,
    /// <see cref="NearbyOptions.AcceptTimeout"/> for an acceptor. The two are separate settings
    /// because the windows measure different spans — an initiator's covers the remote user
    /// deciding, while an acceptor's starts once that decision is made.
    /// </param>
    /// <param name="beforeAwait">
    /// The platform step that starts the handshake, run under the linked token. Runs inside the
    /// <c>try</c> so a synchronous throw still clears the registration.
    /// </param>
    /// <param name="cancellationToken">
    /// The caller's token. Cancellation attributable to it surfaces as
    /// <see cref="OperationCanceledException"/>, never as a timeout.
    /// </param>
    /// <remarks>
    /// <para>
    /// A plugin-owned deadline, not a platform one. iOS has a native invitation timeout, but
    /// Google's Nearby Connections has none at all: <c>requestConnection</c>'s Task completes when
    /// the request is <em>sent</em>, and nothing guarantees a callback ever follows. Without this, a
    /// peer that walks out of range mid-handshake leaves the awaiter suspended forever and strands
    /// its <c>_connectionTcs</c> entry. Applied on both platforms so the observable behaviour
    /// matches.
    /// </para>
    /// <para>
    /// <b>Shared by the connect path and both accept paths.</b> The accept lambdas in
    /// <c>PlatformNearby.android.cs</c> and <c>PlatformNearby.ios.cs</c> previously awaited the
    /// caller's token alone, so an accepted handshake that then stalled — the remote device leaving
    /// range between the request arriving and the connection completing — never returned at all.
    /// Keeping the rule in one shared method is what stops that divergence from recurring in one
    /// partial and not its sibling.
    /// </para>
    /// <para>
    /// Timed through the injected <see cref="TimeProvider"/> so this is testable with
    /// <c>FakeTimeProvider</c> rather than requiring a real 30-second wait — the same pattern as
    /// <c>OutgoingTransfer</c>. <see cref="Timeout.InfiniteTimeSpan"/> is a valid never-firing delay,
    /// so the infinite case needs no separate construction.
    /// </para>
    /// </remarks>
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
            await beforeAwait(timeoutCts.Token);

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
            when (hasTimeout
                && deadlineCts.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested)
        {
            _connectionTcs.TryRemove(device.Id, out _);
            await PlatformAbandonConnectAsync(device);

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
                LogDisposeConnectionError(connection.RemoteDevice.Id, ex);
            }
        }

        PlatformDispose();
        Peers.Clear();
    }
}