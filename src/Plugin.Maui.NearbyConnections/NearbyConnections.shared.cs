using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Discover;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Main interface for nearby connections functionality.
/// Provides centralized event handling and device management.
/// </summary>
public interface INearbyConnections : IDisposable
{
    /// <summary>
    /// An observable stream of processed events from the Nearby Connections API.
    /// Events flow through internal handlers before being exposed externally.
    /// </summary>
    IObservable<INearbyConnectionsEvent> Events { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently advertising.
    /// </summary>
    bool IsAdvertising { get; }

    /// <summary>
    /// Gets a value indicating whether this device is currently discovering nearby devices.
    /// </summary>
    bool IsDiscovering { get; }

    /// <summary>
    /// Gets a collection of currently discovered nearby devices.
    /// </summary>
    IReadOnlyDictionary<string, INearbyDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Gets a collection of currently connected devices.
    /// </summary>
    IReadOnlyDictionary<string, INearbyDevice> ConnectedDevices { get; }

    /// <summary>
    /// Gets or sets the default options to use.
    /// </summary>
    NearbyConnectionsOptions DefaultOptions { get; set; }

    /// <summary>
    /// Start advertising this device.
    /// </summary>
    /// <returns></returns>
    Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin discovery of <see cref="INearbyDevice"/>'s.
    /// </summary>
    /// <returns></returns>
    Task StartDiscoveryAsync(DiscoverOptions? discoverOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop advertising this device.
    /// </summary>
    /// <returns></returns>
    Task StopAdvertisingAsync();

    /// <summary>
    /// Stop discovering nearby devices.
    /// </summary>
    /// <returns></returns>
    Task StopDiscoveryAsync();

    /// <summary>
    /// Send data to a connected device.
    /// </summary>
    /// <param name="deviceId">Target device ID</param>
    /// <param name="data">Data to send</param>
    /// <returns>Task representing the send operation</returns>
    Task SendDataAsync(string deviceId, byte[] data);

    /// <summary>
    /// Accept a connection invitation from a device.
    /// </summary>
    /// <param name="deviceId">Device ID to accept connection from</param>
    /// <returns>Task representing the acceptance operation</returns>
    Task AcceptConnectionAsync(string deviceId);

    /// <summary>
    /// Reject a connection invitation from a device.
    /// </summary>
    /// <param name="deviceId">Device ID to reject connection from</param>
    /// <returns>Task representing the rejection operation</returns>
    Task RejectConnectionAsync(string deviceId);
}

/// <summary>
/// Static entry point for accessing the NearbyConnections API.
/// </summary>
public static class NearbyConnections
{
    static INearbyConnections? s_defaultImplementation;

    internal static void SetDefault(INearbyConnections? implementation) =>
        s_defaultImplementation = implementation;

    /// <summary>
    /// Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Default =>
        s_defaultImplementation ??= new NearbyConnectionsImplementation();
}

/// <summary>
/// Central event hub implementation that processes all nearby connections events internally
/// before exposing them externally. Maintains device state and handles cross-platform abstractions.
/// </summary>
partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly SemaphoreSlim _advertiseSemaphore = new(1, 1);
    readonly SemaphoreSlim _discoverSemaphore = new(1, 1);

    // Internal event processing
    readonly Subject<INearbyConnectionsEvent> _events;
    readonly ConcurrentDictionary<string, INearbyDevice> _discoveredDevices = new();
    readonly ConcurrentDictionary<string, INearbyDevice> _connectedDevices = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConnections = new();

    volatile bool _isAdvertising;
    volatile bool _isDiscovering;
    bool _isDisposed;

    Advertiser? _advertiser;
    Discoverer? _discoverer;

    CancellationTokenRegistration _advertiseCancellationRegistration;
    CancellationTokenRegistration _discoverCancellationRegistration;


    public IObservable<INearbyConnectionsEvent> Events => _events.AsObservable();

    public bool IsAdvertising => _isAdvertising;
    public bool IsDiscovering => _isDiscovering;

    public IReadOnlyDictionary<string, INearbyDevice> DiscoveredDevices => _discoveredDevices;
    public IReadOnlyDictionary<string, INearbyDevice> ConnectedDevices => _connectedDevices;

    public NearbyConnectionsOptions DefaultOptions { get; set; } = new();

    public NearbyConnectionsImplementation()
    {
        _events = new Subject<INearbyConnectionsEvent>();
    }

    /// <summary>
    /// Central hub method that processes all events internally before exposing them externally.
    /// Fully configurable through EventConfiguration.
    /// </summary>
    void ProcessInternalEvent(INearbyConnectionsEvent internalEvent)
    {
        try
        {
            // Handle the event internally (state management, etc.)
            var processedEvent = HandleEventInternally(internalEvent);
            if (processedEvent == null) return;

            // Check if event should be exposed externally
            if (ShouldExposeEvent(processedEvent))
            {
                _events.OnNext(processedEvent);
            }
        }
        catch (Exception ex)
        {
            HandleEventProcessingError(ex, internalEvent);
        }
    }

    /// <summary>
    /// Handle event processing errors using configured error handling.
    /// </summary>
    void HandleEventProcessingError(Exception ex, INearbyConnectionsEvent originalEvent)
    {
        // Use custom error handler if configured
        if (EventConfiguration.CustomErrorHandler != null)
        {
            EventConfiguration.CustomErrorHandler(ex, originalEvent);
            return;
        }

        // Default error handling
        System.Diagnostics.Debug.WriteLine($"Error processing event {originalEvent.GetType().Name}: {ex}");

        // Optionally expose error events externally
        if (EventConfiguration.ExposeProcessingErrors)
        {
            _externalEventSubject.OnNext(new EventProcessingError(
                Guid.NewGuid().ToString(),
                DateTimeOffset.UtcNow,
                originalEvent,
                ex.Message));
        }
    }

    /// <summary>
    /// Internal event handler where you implement your business logic.
    /// This runs before events are exposed externally.
    /// </summary>
    INearbyConnectionsEvent? HandleEventInternally(INearbyConnectionsEvent originalEvent)
    {
        return originalEvent switch
        {
            NearbyDeviceFound deviceFound => HandleDeviceFound(deviceFound),
            NearbyDeviceLost deviceLost => HandleDeviceLost(deviceLost),
            InvitationReceived invitation => HandleInvitationReceived(invitation),
            NearbyDeviceConnected connected => HandleDeviceConnected(connected),
            NearbyDeviceDisconnected disconnected => HandleDeviceDisconnected(disconnected),
            ConnectionFailed failed => HandleConnectionFailed(failed),
            PayloadReceived payload => HandlePayloadReceived(payload),
            AdvertisingStarted advStarted => HandleAdvertisingStarted(advStarted),
            DiscoveryStarted discStarted => HandleDiscoveryStarted(discStarted),
            _ => originalEvent // Pass through other events unchanged
        };
    }

    /// <summary>
    /// Determines if an event should be exposed to external consumers.
    /// Uses configured filtering rules and built-in logic.
    /// </summary>
    bool ShouldExposeEvent(INearbyConnectionsEvent processedEvent)
    {
        // Apply custom filters first
        foreach (var filter in EventConfiguration.EventFilters)
        {
            if (!filter(processedEvent))
                return false;
        }

        // Apply built-in configuration-based filtering
        return processedEvent switch
        {
            // Handle duplicate device found events
            NearbyDeviceFound deviceFound when !EventConfiguration.AllowDuplicateDeviceFoundEvents
                && IsDeviceAlreadyDiscovered(deviceFound.Device) => false,

            // Handle state change events
            AdvertisingStarted or AdvertisingFailed or DiscoveryStarted or DiscoveryFailed
                when !EventConfiguration.ExposeStateChangeEvents => false,

            // Handle error events
            EventProcessingError when !EventConfiguration.ExposeProcessingErrors => false,

            _ => true // Expose all other events by default
        };
    }

    /// <summary>
    /// Check if a device is already discovered using configured comparison logic.
    /// </summary>
    bool IsDeviceAlreadyDiscovered(INearbyDevice device)
    {
        if (EventConfiguration.CustomDeviceComparer != null)
        {
            return _discoveredDevices.Values.Any(existing =>
                EventConfiguration.CustomDeviceComparer(existing, device));
        }

        // Default: compare by ID
        return _discoveredDevices.ContainsKey(device.Id);
    }

    void OnEventError(Exception error)
    {
        System.Diagnostics.Debug.WriteLine($"Event stream error: {error}");
        _events.OnError(error);
    }

    #region Internal Event Handlers
    /// <summary>
    /// Handle device found event - configurable state management
    /// </summary>
    NearbyDeviceFound HandleDeviceFound(NearbyDeviceFound deviceFound)
    {
        if (EventConfiguration.AutoManageDeviceState)
        {
            // Check device limit
            if (EventConfiguration.MaxDiscoveredDevices > 0 &&
                _discoveredDevices.Count >= EventConfiguration.MaxDiscoveredDevices)
            {
                // Remove oldest device
                var oldestDevice = _deviceDiscoveryTimes.OrderBy(kvp => kvp.Value).First();
                _discoveredDevices.TryRemove(oldestDevice.Key, out _);
                _deviceDiscoveryTimes.TryRemove(oldestDevice.Key, out _);
            }

            _discoveredDevices.TryAdd(deviceFound.Device.Id, deviceFound.Device);
            _deviceDiscoveryTimes.TryAdd(deviceFound.Device.Id, DateTimeOffset.UtcNow);

            NotifyStateChange("DeviceDiscovered", deviceFound.Device);
        }

        return deviceFound;
    }

    /// <summary>
    /// Handle device lost event - configurable state management
    /// </summary>
    NearbyDeviceLost HandleDeviceLost(NearbyDeviceLost deviceLost)
    {
        if (EventConfiguration.AutoManageDeviceState)
        {
            _discoveredDevices.TryRemove(deviceLost.Device.Id, out _);
            _deviceDiscoveryTimes.TryRemove(deviceLost.Device.Id, out _);

            NotifyStateChange("DeviceLost", deviceLost.Device);
        }

        return deviceLost;
    }

    /// <summary>
    /// Handle invitation received - configurable connection management
    /// </summary>
    InvitationReceived HandleInvitationReceived(InvitationReceived invitation)
    {
        if (EventConfiguration.AutoCompletePendingConnections)
        {
            _pendingConnections.TryAdd(invitation.From.Id, new TaskCompletionSource<bool>());
        }

        NotifyStateChange("InvitationReceived", invitation.From);
        return invitation;
    }

    /// <summary>
    /// Handle device connected - configurable state management
    /// </summary>
    NearbyDeviceConnected HandleDeviceConnected(NearbyDeviceConnected connected)
    {
        if (EventConfiguration.AutoManageDeviceState)
        {
            _connectedDevices.TryAdd(connected.Device.Id, connected.Device);
            _discoveredDevices.TryRemove(connected.Device.Id, out _);
            _deviceDiscoveryTimes.TryRemove(connected.Device.Id, out _);

            NotifyStateChange("DeviceConnected", connected.Device);
        }

        // Complete any pending connection tasks
        if (EventConfiguration.AutoCompletePendingConnections &&
            _pendingConnections.TryRemove(connected.Device.Id, out var tcs))
        {
            tcs.SetResult(true);
        }

        return connected;
    }

    /// <summary>
    /// Handle device disconnected - configurable state management
    /// </summary>
    NearbyDeviceDisconnected HandleDeviceDisconnected(NearbyDeviceDisconnected disconnected)
    {
        if (EventConfiguration.AutoManageDeviceState)
        {
            _connectedDevices.TryRemove(disconnected.Device.Id, out _);
            NotifyStateChange("DeviceDisconnected", disconnected.Device);
        }

        return disconnected;
    }

    /// <summary>
    /// Handle connection failed - configurable connection management
    /// </summary>
    ConnectionFailed HandleConnectionFailed(ConnectionFailed failed)
    {
        if (EventConfiguration.AutoCompletePendingConnections &&
            _pendingConnections.TryRemove(failed.Device.Id, out var tcs))
        {
            tcs.SetException(new InvalidOperationException(failed.Reason));
        }

        NotifyStateChange("ConnectionFailed", failed.Device);
        return failed;
    }

    /// <summary>
    /// Handle payload received - configurable processing
    /// </summary>
    PayloadReceived HandlePayloadReceived(PayloadReceived payload)
    {
        NotifyStateChange("PayloadReceived", payload);
        return payload;
    }

    /// <summary>
    /// Handle advertising started
    /// </summary>
    AdvertisingStarted HandleAdvertisingStarted(AdvertisingStarted advStarted)
    {
        NotifyStateChange("AdvertisingStarted", advStarted.ServiceName);
        return advStarted;
    }

    /// <summary>
    /// Handle discovery started
    /// </summary>
    DiscoveryStarted HandleDiscoveryStarted(DiscoveryStarted discStarted)
    {
        NotifyStateChange("DiscoveryStarted", discStarted.ServiceName);
        return discStarted;
    }

    /// <summary>
    /// Notify state change callbacks if configured.
    /// </summary>
    void NotifyStateChange(string changeType, object? data)
    {
        foreach (var callback in EventConfiguration.StateChangeCallbacks)
        {
            try
            {
                callback(changeType, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"State change callback error: {ex}");
            }
        }
    }

    /// <summary>
    /// Clean up expired discovered devices based on configuration.
    /// </summary>
    void CleanupExpiredDevices(object? state)
    {
        if (!EventConfiguration.DiscoveredDeviceTimeout.HasValue) return;

        var expiredTime = DateTimeOffset.UtcNow - EventConfiguration.DiscoveredDeviceTimeout.Value;
        var expiredDevices = _deviceDiscoveryTimes
            .Where(kvp => kvp.Value < expiredTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var deviceId in expiredDevices)
        {
            if (_discoveredDevices.TryRemove(deviceId, out var device))
            {
                _deviceDiscoveryTimes.TryRemove(deviceId, out _);
                NotifyStateChange("DeviceExpired", device);

                // Optionally emit device lost event
                if (EventConfiguration.ExposeStateChangeEvents)
                {
                    var lostEvent = new NearbyDeviceLost(
                        Guid.NewGuid().ToString(),
                        DateTimeOffset.UtcNow,
                        device);

                    _externalEventSubject.OnNext(lostEvent);
                }
            }
        }
    }
    #endregion

    #region Public API Methods

    public async Task StartAdvertisingAsync(AdvertiseOptions? advertiseOptions = default, CancellationToken cancellationToken = default)
    {
        await _advertiseSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (_isAdvertising)
            {
                throw new InvalidOperationException($"Already advertising. Call {nameof(StopAdvertisingAsync)} or request cancellation first.");
            }

            await _advertiseCancellationRegistration.DisposeAsync();

            _advertiseCancellationRegistration = cancellationToken.Register(
                static state => ((INearbyConnections)state!).StopAdvertisingAsync().ConfigureAwait(false).GetAwaiter().GetResult(),
                this,
                useSynchronizationContext: false);

            try
            {
                _advertiser = new Advertiser(this);
                await _advertiser.StartAdvertisingAsync(advertiseOptions ?? DefaultOptions.AdvertiserOptions);
                _isAdvertising = true;
            }
            catch
            {
                _advertiser?.Dispose();
                _advertiser = null;
                _isAdvertising = false;
                await _advertiseCancellationRegistration.DisposeAsync();
                throw;
            }
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
            if (_isDiscovering)
            {
                throw new InvalidOperationException($"Already discovering. Call {nameof(StopDiscoveryAsync)} or request cancellation first.");
            }

            await _discoverCancellationRegistration.DisposeAsync();

            _discoverCancellationRegistration = cancellationToken.Register(
                static state => ((INearbyConnections)state!).StopDiscoveryAsync().ConfigureAwait(false).GetAwaiter().GetResult(),
                this,
                useSynchronizationContext: false);

            try
            {
                _discoverer = new Discoverer(this);
                await _discoverer.StartDiscoveringAsync(discoverOptions ?? DefaultOptions.DiscovererOptions);
                _isDiscovering = true;
            }
            catch
            {
                _discoverer?.Dispose();
                _discoverer = null;
                _isDiscovering = false;
                await _discoverCancellationRegistration.DisposeAsync();
                throw;
            }
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
            if (_isAdvertising)
            {
                _advertiser?.StopAdvertising();
                _advertiser?.Dispose();
                _advertiser = null;
                _isAdvertising = false;
                await _advertiseCancellationRegistration.DisposeAsync();
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
            if (_isDiscovering)
            {
                _discoverer?.StopDiscovering();
                _discoverer?.Dispose();
                _discoverer = null;
                _isDiscovering = false;
                await _discoverCancellationRegistration.DisposeAsync();
            }
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public async Task SendDataAsync(string deviceId, byte[] data)
    {
        if (!_connectedDevices.ContainsKey(deviceId))
        {
            throw new InvalidOperationException($"Device {deviceId} is not connected");
        }

        // TODO: Implement platform-specific data sending
        await PlatformSendDataAsync(deviceId, data);
    }

    public async Task AcceptConnectionAsync(string deviceId)
    {
        if (!_pendingConnections.ContainsKey(deviceId))
        {
            throw new InvalidOperationException($"No pending connection for device {deviceId}");
        }

        // TODO: Implement platform-specific connection acceptance
        await PlatformAcceptConnectionAsync(deviceId);
    }

    public async Task RejectConnectionAsync(string deviceId)
    {
        if (!_pendingConnections.ContainsKey(deviceId))
        {
            throw new InvalidOperationException($"No pending connection for device {deviceId}");
        }

        // TODO: Implement platform-specific connection rejection
        await PlatformRejectConnectionAsync(deviceId);

        // Clean up pending connection
        if (_pendingConnections.TryRemove(deviceId, out var tcs))
        {
            tcs.SetCanceled();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                try
                {
                    _advertiseSemaphore?.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch { /* Ignore */ }

                _advertiser?.StopAdvertising();
                _advertiser?.Dispose();
                _advertiser = null;
                _advertiseSemaphore?.Dispose();

                try
                {
                    _discoverSemaphore?.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch { /* Ignore */ }

                _discoverer?.StopDiscovering();
                _discoverer?.Dispose();
                _discoverer = null;
                _discoverSemaphore?.Dispose();

                _events?.OnCompleted();
                _events?.Dispose();
            }

            _isDisposed = true;
        }
    }

    ~NearbyConnectionsImplementation()
        => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
