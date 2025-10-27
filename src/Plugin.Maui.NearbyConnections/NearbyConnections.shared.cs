using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Static access to the Plugin.Maui.NearbyConnections API.
/// </summary>
public static class NearbyConnections
{
    static readonly Lock s_lock = new();

    static INearbyConnections? s_defaultImplementation;
    static NearbyConnectionsOptions? s_defaultOptions;

    internal static void SetDefault(INearbyConnections? implementation) =>
        s_defaultImplementation = implementation;

    /// <summary>
    /// Configure options for the static Default instance.
    /// Must be called before accessing Default for the first time.
    /// </summary>
    /// <param name="configure">Action to configure options</param>
    public static void Configure(Action<NearbyConnectionsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        lock (s_lock)
        {
            if (s_defaultImplementation != null)
            {
                throw new InvalidOperationException(
                    "Cannot configure after Default has been accessed. Call Configure() before using Default.");
            }

            s_defaultOptions = new NearbyConnectionsOptions();
            configure(s_defaultOptions);
        }
    }

    /// <summary>
    /// Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Default
    {
        get
        {
            if (s_defaultImplementation is null)
            {
                lock (s_lock)
                {
                    s_defaultImplementation ??= new NearbyConnectionsImplementation(
                        s_defaultOptions ?? new NearbyConnectionsOptions(),
                        serviceProvider: null);
                }
            }
            return s_defaultImplementation;
        }
    }
}

/// <summary>
/// Central event hub implementation that processes all nearby connections events internally
/// before exposing them externally. Maintains device state and handles cross-platform abstractions.
/// </summary>
partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly SemaphoreSlim _advertiseSemaphore = new(1, 1);
    readonly SemaphoreSlim _discoverSemaphore = new(1, 1);

    readonly Subject<INearbyConnectionsEvent> _events;
    readonly ConcurrentDictionary<string, INearbyDevice> _discoveredDevices = new();
    readonly ConcurrentDictionary<string, INearbyDevice> _connectedDevices = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConnections = new();
    bool _isDisposed;

    Advertiser? _advertiser;
    Discoverer? _discoverer;

    public IObservable<INearbyConnectionsEvent> Events => _events.AsObservable();

    public bool IsAdvertising { get; private set; }
    public bool IsDiscovering { get; private set; }

    public IReadOnlyDictionary<string, INearbyDevice> DiscoveredDevices => _discoveredDevices;
    public IReadOnlyDictionary<string, INearbyDevice> ConnectedDevices => _connectedDevices;

    public NearbyConnectionsOptions DefaultOptions { get; }

    /// <summary>
    /// Creates a new instance with default options (for static API usage).
    /// </summary>
    public NearbyConnectionsImplementation()
        : this(new NearbyConnectionsOptions(), serviceProvider: null)
    {
    }

    /// <summary>
    /// Creates a new instance with specified options (for DI usage).
    /// </summary>
    /// <param name="options">Configuration options</param>
    /// <param name="serviceProvider">Optional service provider for resolving processors</param>
    public NearbyConnectionsImplementation(NearbyConnectionsOptions options, IServiceProvider? serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        DefaultOptions = options;
        _events = new Subject<INearbyConnectionsEvent>();
    }

    /// <summary>
    /// Process an event through the internal pipeline before exposing it to external subscribers.
    /// Follows Sentry's pattern of wrapping processor calls in exception handling.
    /// </summary>
    /// <param name="evt">The event to process</param>
    void ProcessEvent(INearbyConnectionsEvent evt)
    {
        if (evt is null)
        {
            return;
        }

        try
        {
            _events.OnNext(evt);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NEARBY CONNECTIONS] Exception in event subscriber for event {evt.EventId}: {ex.Message}");
        }
    }

    public async Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null, CancellationToken cancellationToken = default)
    {
        await _advertiseSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (IsAdvertising)
            {
                throw new InvalidOperationException($"Already advertising. Call {nameof(StopAdvertisingAsync)} first.");
            }

            _advertiser = new Advertiser(this);
            await _advertiser.StartAdvertisingAsync(advertiseOptions ?? DefaultOptions.AdvertiserOptions);
            IsAdvertising = true;
        }
        catch
        {
            _advertiser?.Dispose();
            _advertiser = null;
            IsAdvertising = false;
            throw;
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null, CancellationToken cancellationToken = default)
    {
        await _discoverSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (IsDiscovering)
            {
                throw new InvalidOperationException($"Already discovering. Call {nameof(StopDiscoveryAsync)} first.");
            }

            _discoverer = new Discoverer(this);
            await _discoverer.StartDiscoveringAsync(discoverOptions ?? DefaultOptions.DiscovererOptions);
            IsDiscovering = true;
        }
        catch
        {
            _discoverer?.Dispose();
            _discoverer = null;
            IsDiscovering = false;
            throw;
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public async Task StopAdvertisingAsync()
    {
        await _advertiseSemaphore.WaitAsync();
        try
        {
            if (IsAdvertising)
            {
                _advertiser?.StopAdvertising();
                _advertiser?.Dispose();
                _advertiser = null;
                IsAdvertising = false;
            }
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StopDiscoveryAsync()
    {
        await _discoverSemaphore.WaitAsync();
        try
        {
            if (IsDiscovering)
            {
                _discoverer?.StopDiscovering();
                _discoverer?.Dispose();
                _discoverer = null;
                IsDiscovering = false;
            }
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                _advertiseSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NEARBY CONNECTIONS] Dispose: Failed to acquire advertise semaphore: {ex}");
            }

            _advertiser?.StopAdvertising();
            _advertiser?.Dispose();
            _advertiser = null;
            _advertiseSemaphore?.Dispose();

            try
            {
                _discoverSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NEARBY CONNECTIONS] Dispose: Failed to acquire discover semaphore: {ex}");
            }

            _discoverer?.StopDiscovering();
            _discoverer?.Dispose();
            _discoverer = null;
            _discoverSemaphore?.Dispose();

            _events?.OnCompleted();
            _events?.Dispose();
        }

        _isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
