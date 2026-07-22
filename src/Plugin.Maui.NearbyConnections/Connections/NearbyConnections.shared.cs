using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly INearbyDeviceManager _deviceManager;
    readonly ILogger _logger;

    internal Channel<NearbyConnectionRequest> _advertiseChannel;
    internal Channel<NearbyDeviceEvent> _discoverChannel;
    internal readonly ConcurrentDictionary<string, (TaskCompletionSource<NearbyConnection> Tcs, CancellationToken Ct)> _connectionTcs;
    internal readonly ConcurrentDictionary<string, NearbyConnection> _activeConnections;

#if IOS
    internal PeerIdManager PeerIdManager { get; init; }
#endif

    bool _isDisposed;

    internal TimeProvider TimeProvider { get; }

    public NearbyConnectionsOptions Options { get; }

    internal NearbyConnectionsImplementation(
        INearbyDeviceManager deviceManager,
        TimeProvider timeProvider,
        NearbyConnectionsOptions options,
        ILogger logger
#if IOS
        , PeerIdManager peerIdManager
#endif
        )
    {
        ArgumentNullException.ThrowIfNull(deviceManager);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _deviceManager = deviceManager;
        TimeProvider = timeProvider;
        Options = options;
        _logger = logger;
#if IOS
        PeerIdManager = peerIdManager;
#endif

        var channelOptions = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
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
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
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
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
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
    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return ValueTask.CompletedTask;
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

        PlatformDispose();
        _deviceManager.Clear();
        _isDisposed = true;

        return ValueTask.CompletedTask;
    }
}
