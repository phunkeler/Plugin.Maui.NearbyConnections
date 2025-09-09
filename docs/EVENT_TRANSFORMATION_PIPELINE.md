# Event Transformation Pipeline Implementation Guide

This document outlines the design and implementation of a flexible event transformation pipeline for the NearbyConnections plugin. This system transforms platform-specific callback/delegate arguments into cross-platform events and exposes them through an `IObservable<INearbyConnectionsEvent>` interface.

## Architecture Overview

```
Platform Callbacks → Event Adapters )Optional) → Event Processors (Optional) → Session Observable
     │                    │               │                    │
     ▼                    ▼               ▼                    ▼
[iOS Delegates]    [PlatformEvent]   [Cross-platform]    [IObservable<T>]
[Android Callbacks]  →  [Transformation] → [Event Objects]  →  [for consumers]
                                          │
                                          ▼
                                   [Optional Pipeline]
                                   [Processing Steps]
```

## Design Principles

1. **Separation of Concerns**: Platform adaptation, transformation, and processing are separate layers
2. **Type Safety**: Strong typing throughout with generic constraints
3. **Extensibility**: Easy to add new event types and processing steps
4. **Testability**: Each component can be unit tested in isolation
5. **Performance**: Minimal allocations and efficient event routing
6. **Flexibility**: Support both adapter-based transformation and direct event publishing

## Implementation Steps

### 1. Core Event Infrastructure

#### Base Event Interface and Classes

```csharp
// File: Events/INearbyConnectionsEvent.cs
namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Base interface for all nearby connections events.
/// </summary>
public interface INearbyConnectionsEvent
{
    string EventId { get; }
    DateTimeOffset Timestamp { get; }
    EventCategory Category { get; }
    string? CorrelationId { get; }
}

public enum EventCategory
{
    Discovery,
    Advertising,
    Connection,
    DataTransfer,
    Error,
    Lifecycle
}

/// <summary>
/// Base implementation providing common event functionality.
/// </summary>
public abstract class NearbyConnectionsEventBase : INearbyConnectionsEvent
{
    protected NearbyConnectionsEventBase(EventCategory category, string? correlationId = null)
    {
        EventId = Guid.NewGuid().ToString();
        Timestamp = DateTimeOffset.UtcNow;
        Category = category;
        CorrelationId = correlationId;
    }

    public string EventId { get; }
    public DateTimeOffset Timestamp { get; }
    public EventCategory Category { get; }
    public string? CorrelationId { get; }
}
```

#### Specific Event Types

```csharp
// File: Events/DeviceDiscoveredEvent.cs
public sealed class DeviceDiscoveredEvent : NearbyConnectionsEventBase
{
    public DeviceDiscoveredEvent(INearbyDevice device, string? correlationId = null)
        : base(EventCategory.Discovery, correlationId)
    {
        Device = device;
    }

    public INearbyDevice Device { get; }
}

// File: Events/ConnectionStateChangedEvent.cs
public sealed class ConnectionStateChangedEvent : NearbyConnectionsEventBase
{
    public ConnectionStateChangedEvent(string deviceId, ConnectionState previousState,
        ConnectionState currentState, string? correlationId = null)
        : base(EventCategory.Connection, correlationId)
    {
        DeviceId = deviceId;
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public string DeviceId { get; }
    public ConnectionState PreviousState { get; }
    public ConnectionState CurrentState { get; }
}

// File: Events/DeviceLostEvent.cs
public sealed class DeviceLostEvent : NearbyConnectionsEventBase
{
    public DeviceLostEvent(string deviceId, DateTimeOffset lostAt, string? correlationId = null)
        : base(EventCategory.Discovery, correlationId)
    {
        DeviceId = deviceId;
        LostAt = lostAt;
    }

    public string DeviceId { get; }
    public DateTimeOffset LostAt { get; }
}

// File: Events/DiscoveryErrorEvent.cs
public sealed class DiscoveryErrorEvent : NearbyConnectionsEventBase
{
    public DiscoveryErrorEvent(string message, int errorCode, string domain, string? correlationId = null)
        : base(EventCategory.Error, correlationId)
    {
        Message = message;
        ErrorCode = errorCode;
        Domain = domain;
    }

    public string Message { get; }
    public int ErrorCode { get; }
    public string Domain { get; }
}
```

### 2. Platform Event Adaptation Layer

#### Event Adapter Interface

```csharp
// File: Events/Adapters/IEventAdapter.cs
namespace Plugin.Maui.NearbyConnections.Events.Adapters;

/// <summary>
/// Transforms platform-specific arguments into cross-platform events.
/// </summary>
public interface IEventAdapter<in TPlatformArgs, out TEvent>
    where TEvent : INearbyConnectionsEvent
{
    TEvent? Transform(TPlatformArgs platformArgs, EventTransformContext context);
}

/// <summary>
/// Context information available during event transformation.
/// </summary>
public sealed class EventTransformContext
{
    public EventTransformContext(string sessionId, string? correlationId = null)
    {
        SessionId = sessionId;
        CorrelationId = correlationId;
        Properties = new Dictionary<string, object>();
    }

    public string SessionId { get; }
    public string? CorrelationId { get; }
    public Dictionary<string, object> Properties { get; }
}
```

#### Cross-Platform Event Args

```csharp
// File: Events/PlatformEventArgs.cs
namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Cross-platform arguments for peer discovery events.
/// </summary>
public sealed class PeerDiscoveredEventArgs
{
    public PeerDiscoveredEventArgs(string peerId, string displayName, Dictionary<string, object>? metadata = null)
    {
        PeerId = peerId ?? throw new ArgumentNullException(nameof(peerId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    public string PeerId { get; }
    public string DisplayName { get; }
    public Dictionary<string, object> Metadata { get; }
}

/// <summary>
/// Cross-platform arguments for connection state changes.
/// </summary>
public sealed class ConnectionStateChangedEventArgs
{
    public ConnectionStateChangedEventArgs(string deviceId, ConnectionState previousState, ConnectionState currentState)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public string DeviceId { get; }
    public ConnectionState PreviousState { get; }
    public ConnectionState CurrentState { get; }
}
```

#### Cross-Platform Adapters

```csharp
// File: Events/Adapters/PeerDiscoveredAdapter.cs
/// <summary>
/// Cross-platform adapter for peer discovery events.
/// </summary>
public sealed class PeerDiscoveredAdapter : IEventAdapter<PeerDiscoveredEventArgs, DeviceDiscoveredEvent>
{
    public DeviceDiscoveredEvent? Transform(PeerDiscoveredEventArgs platformArgs, EventTransformContext context)
    {
        var device = new CrossPlatformNearbyDevice(
            id: platformArgs.PeerId,
            name: platformArgs.DisplayName,
            metadata: platformArgs.Metadata,
            discoveredAt: DateTimeOffset.UtcNow
        );

        return new DeviceDiscoveredEvent(device, context.CorrelationId);
    }
}

// File: Events/Adapters/ConnectionStateAdapter.cs
/// <summary>
/// Cross-platform adapter for connection state changes.
/// </summary>
public sealed class ConnectionStateAdapter : IEventAdapter<ConnectionStateChangedEventArgs, ConnectionStateChangedEvent>
{
    public ConnectionStateChangedEvent? Transform(ConnectionStateChangedEventArgs platformArgs, EventTransformContext context)
    {
        return new ConnectionStateChangedEvent(
            platformArgs.DeviceId,
            platformArgs.PreviousState,
            platformArgs.CurrentState,
            context.CorrelationId
        );
    }
}
```

### 3. Event Processing Pipeline

#### Pipeline Interfaces

```csharp
// File: Events/Pipeline/IEventProcessor.cs
namespace Plugin.Maui.NearbyConnections.Events.Pipeline;

/// <summary>
/// Processes events through a transformation pipeline.
/// </summary>
public interface IEventProcessor<TEvent> where TEvent : INearbyConnectionsEvent
{
    ValueTask<TEvent?> ProcessAsync(TEvent eventItem, EventProcessingContext context);
}

/// <summary>
/// Context available during event processing.
/// </summary>
public sealed class EventProcessingContext
{
    public EventProcessingContext(string sessionId, CancellationToken cancellationToken = default)
    {
        SessionId = sessionId;
        CancellationToken = cancellationToken;
        Properties = new Dictionary<string, object>();
    }

    public string SessionId { get; }
    public CancellationToken CancellationToken { get; }
    public Dictionary<string, object> Properties { get; }
}

/// <summary>
/// Orchestrates multiple processors in a pipeline.
/// </summary>
public interface IEventPipeline<TEvent> where TEvent : INearbyConnectionsEvent
{
    ValueTask<TEvent?> ProcessAsync(TEvent eventItem, EventProcessingContext context);
    IEventPipeline<TEvent> AddProcessor(IEventProcessor<TEvent> processor);
}
```

#### Pipeline Implementation

```csharp
// File: Events/Pipeline/EventPipeline.cs
/// <summary>
/// Default pipeline implementation that processes events sequentially.
/// </summary>
public sealed class EventPipeline<TEvent> : IEventPipeline<TEvent>
    where TEvent : INearbyConnectionsEvent
{
    private readonly List<IEventProcessor<TEvent>> _processors = new();

    public async ValueTask<TEvent?> ProcessAsync(TEvent eventItem, EventProcessingContext context)
    {
        var current = eventItem;

        foreach (var processor in _processors)
        {
            if (current == null) break;

            try
            {
                current = await processor.ProcessAsync(current, context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pipeline processor error: {ex}");
            }
        }

        return current;
    }

    public IEventPipeline<TEvent> AddProcessor(IEventProcessor<TEvent> processor)
    {
        _processors.Add(processor);
        return this;
    }
}
```

#### Common Event Processors

```csharp
// File: Events/Pipeline/CorrelationProcessor.cs
/// <summary>
/// Adds correlation tracking to events.
/// </summary>
public sealed class CorrelationProcessor<TEvent> : IEventProcessor<TEvent>
    where TEvent : INearbyConnectionsEvent
{
    public ValueTask<TEvent?> ProcessAsync(TEvent eventItem, EventProcessingContext context)
    {
        if (eventItem is ConnectionStateChangedEvent connectionEvent)
        {
            context.Properties[$"Connection_{connectionEvent.DeviceId}"] = connectionEvent.CurrentState;
        }

        return ValueTask.FromResult<TEvent?>(eventItem);
    }
}

// File: Events/Pipeline/DeduplicationProcessor.cs
/// <summary>
/// Prevents duplicate events from being emitted.
/// </summary>
public sealed class DeduplicationProcessor<TEvent> : IEventProcessor<TEvent>
    where TEvent : INearbyConnectionsEvent
{
    private readonly HashSet<string> _seenEvents = new();
    private readonly TimeSpan _windowSize;

    public DeduplicationProcessor(TimeSpan windowSize)
    {
        _windowSize = windowSize;
    }

    public ValueTask<TEvent?> ProcessAsync(TEvent eventItem, EventProcessingContext context)
    {
        var key = GenerateDeduplicationKey(eventItem);

        if (_seenEvents.Contains(key))
        {
            return ValueTask.FromResult<TEvent?>(null);
        }

        _seenEvents.Add(key);
        _ = Task.Delay(_windowSize).ContinueWith(_ => _seenEvents.Remove(key));

        return ValueTask.FromResult<TEvent?>(eventItem);
    }

    private static string GenerateDeduplicationKey(TEvent eventItem)
    {
        return eventItem switch
        {
            DeviceDiscoveredEvent discovery => $"discovery_{discovery.Device.Id}",
            ConnectionStateChangedEvent connection => $"connection_{connection.DeviceId}_{connection.CurrentState}",
            _ => eventItem.EventId
        };
    }
}
```

### 4. Event Publisher

#### Event Publisher Interface and Implementation

```csharp
// File: Events/IEventPublisher.cs
namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Aggregates events from multiple sources and exposes them through IObservable.
/// </summary>
public interface IEventPublisher : IDisposable
{
    IObservable<INearbyConnectionsEvent> Events { get; }
    ValueTask PublishAsync<TEvent>(TEvent eventItem) where TEvent : INearbyConnectionsEvent;
    ValueTask PublishPlatformEventAsync<TPlatformArgs>(TPlatformArgs platformArgs, EventTransformContext? transformContext = null);
    void RegisterAdapter<TPlatformArgs, TEvent>(IEventAdapter<TPlatformArgs, TEvent> adapter) where TEvent : INearbyConnectionsEvent;
}

// File: Events/EventPublisher.cs
/// <summary>
/// Thread-safe event publisher implementation using Reactive Extensions.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly Subject<INearbyConnectionsEvent> _eventSubject = new();
    private readonly EventProcessingContext _context;
    private readonly IEventPipeline<INearbyConnectionsEvent> _pipeline;
    private readonly Dictionary<Type, object> _adapters = new();
    private volatile bool _disposed;

    public EventPublisher(string sessionId, IEventPipeline<INearbyConnectionsEvent>? pipeline = null)
    {
        _context = new EventProcessingContext(sessionId);
        _pipeline = pipeline ?? CreateDefaultPipeline();
    }

    public IObservable<INearbyConnectionsEvent> Events => _eventSubject.AsObservable();

    // Direct event publishing (bypasses adapters)
    public async ValueTask PublishAsync<TEvent>(TEvent eventItem) where TEvent : INearbyConnectionsEvent
    {
        if (_disposed) return;

        try
        {
            var processedEvent = await _pipeline.ProcessAsync(eventItem, _context);
            if (processedEvent != null)
            {
                _eventSubject.OnNext(processedEvent);
            }
        }
        catch (Exception ex)
        {
            _eventSubject.OnError(ex);
        }
    }

    // Adapter-based event publishing
    public async ValueTask PublishPlatformEventAsync<TPlatformArgs>(TPlatformArgs platformArgs,
        EventTransformContext? transformContext = null)
    {
        if (_disposed || !_adapters.TryGetValue(typeof(TPlatformArgs), out var adapter))
            return;

        if (adapter is IEventAdapter<TPlatformArgs, INearbyConnectionsEvent> typedAdapter)
        {
            transformContext ??= new EventTransformContext(_context.SessionId);
            var crossPlatformEvent = typedAdapter.Transform(platformArgs, transformContext);

            if (crossPlatformEvent != null)
            {
                await PublishAsync(crossPlatformEvent);
            }
        }
    }

    public void RegisterAdapter<TPlatformArgs, TEvent>(IEventAdapter<TPlatformArgs, TEvent> adapter)
        where TEvent : INearbyConnectionsEvent
    {
        _adapters[typeof(TPlatformArgs)] = adapter;
    }

    private static IEventPipeline<INearbyConnectionsEvent> CreateDefaultPipeline()
    {
        return new EventPipeline<INearbyConnectionsEvent>()
            .AddProcessor(new CorrelationProcessor<INearbyConnectionsEvent>())
            .AddProcessor(new DeduplicationProcessor<INearbyConnectionsEvent>(TimeSpan.FromSeconds(1)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _eventSubject.OnCompleted();
        _eventSubject.Dispose();
    }
}
```

### 5. Session Integration

#### Updated Session Options

```csharp
// File: Session/NearbyConnectionsSessionOptions.cs - Add EventProviderOptions
public class EventProviderOptions
{
    public bool EnableCorrelation { get; set; } = true;
    public bool EnableDeduplication { get; set; } = true;
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMilliseconds(500);
    public List<IEventProcessor<INearbyConnectionsEvent>> CustomProcessors { get; set; } = new();
}
```

#### Updated Session Implementation

```csharp
// File: Session/NearbyConnectionsSession.cs - Update implementation
public class NearbyConnectionsSession : INearbyConnectionsSession
{
    private readonly EventPublisher _eventPublisher;

    public NearbyConnectionsSession(
        NearbyConnectionsSessionOptions options,
        IAdvertiserFactory advertiserFactory,
        IDiscovererFactory discovererFactory)
    {
        _options = options;
        _advertiserFactory = advertiserFactory;
        _discovererFactory = discovererFactory;

        // Initialize event system
        _eventPublisher = new EventPublisher(
            sessionId: Guid.NewGuid().ToString(),
            pipeline: CreateEventPipeline(options.EventProviderOptions)
        );

        RegisterPlatformAdapters();
    }

    public IObservable<INearbyConnectionsEvent> Events => _eventPublisher.Events;

    private void RegisterPlatformAdapters()
    {
        // Register adapters for complex transformations only
        _eventPublisher.RegisterAdapter(new PeerDiscoveredAdapter());
        _eventPublisher.RegisterAdapter(new ConnectionStateAdapter());
    }

    private IEventPipeline<INearbyConnectionsEvent> CreateEventPipeline(EventProviderOptions options)
    {
        var pipeline = new EventPipeline<INearbyConnectionsEvent>();

        if (options.EnableCorrelation)
            pipeline.AddProcessor(new CorrelationProcessor<INearbyConnectionsEvent>());

        if (options.EnableDeduplication)
            pipeline.AddProcessor(new DeduplicationProcessor<INearbyConnectionsEvent>(options.DeduplicationWindow));

        return pipeline;
    }

    public void Dispose()
    {
        _eventPublisher?.Dispose();
        // ... other cleanup
    }
}
```

### 6. Platform Implementation Examples

#### iOS Discoverer Implementation

```csharp
// File: Discover/Discoverer.ios.cs - Updated implementation
public partial class Discoverer : NSObject, IMCNearbyServiceBrowserDelegate
{
    private readonly EventPublisher _eventPublisher;
    private readonly string _sessionId;

    public Discoverer(EventPublisher eventPublisher, string sessionId)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _sessionId = sessionId;
    }

    /// <inheritdoc/>
    public void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary? info)
    {
        Console.WriteLine($"[DISCOVERER] 🎉 SUCCESS: Found peer: {peerID.DisplayName}");

        try
        {
            // Complex transformation - use adapter
            using var serializedPeerId = _myMCPeerIDManager.ArchivePeerId(peerID);
            var bytes = serializedPeerId.ToArray();
            var uniqueId = Convert.ToBase64String(bytes);

            var metadata = ExtractMetadataFromNSDictionary(info);

            var crossPlatformArgs = new PeerDiscoveredEventArgs(
                peerId: uniqueId,
                displayName: peerID.DisplayName,
                metadata: metadata
            );

            _ = _eventPublisher.PublishPlatformEventAsync(crossPlatformArgs,
                new EventTransformContext(_sessionId, correlationId: "peer_discovery"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DISCOVERER] Error processing found peer: {ex}");
        }
    }

    /// <inheritdoc/>
    public void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
    {
        Console.WriteLine($"[DISCOVERER] ❌ Lost peer: {peerID.DisplayName}");

        // Simple case - create event directly
        var deviceLostEvent = new DeviceLostEvent(peerID.DisplayName, DateTimeOffset.UtcNow);
        _ = _eventPublisher.PublishAsync(deviceLostEvent);
    }

    /// <inheritdoc/>
    public void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
    {
        Console.WriteLine($"[DISCOVERER] ERROR: Failed to start browsing: {error.LocalizedDescription}");

        // Simple case - create event directly
        var errorEvent = new DiscoveryErrorEvent(
            error.LocalizedDescription,
            (int)error.Code,
            error.Domain
        );

        _ = _eventPublisher.PublishAsync(errorEvent);
    }

    private Dictionary<string, object> ExtractMetadataFromNSDictionary(NSDictionary? info)
    {
        var metadata = new Dictionary<string, object>();

        if (info != null)
        {
            foreach (var key in info.Keys)
            {
                if (key is NSString nsKey && info[key] is { } value)
                {
                    metadata[nsKey.ToString()] = ConvertNSObjectToNetType(value);
                }
            }
        }

        return metadata;
    }

    private static object ConvertNSObjectToNetType(NSObject nsObject)
    {
        return nsObject switch
        {
            NSString nsString => nsString.ToString(),
            NSNumber nsNumber => nsNumber.DoubleValue,
            NSData nsData => nsData.ToArray(),
            _ => nsObject.ToString() ?? string.Empty
        };
    }
}
```

#### Android Discoverer Implementation

```csharp
// File: Discover/Discoverer.android.cs - Updated implementation
public partial class Discoverer
{
    private readonly EventPublisher _eventPublisher;
    private readonly string _sessionId;

    public Discoverer(EventPublisher eventPublisher, string sessionId)
    {
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _sessionId = sessionId;
    }

    private void OnEndpointFound(string endpointId, DiscoveredEndpointInfo info)
    {
        Console.WriteLine($"[DISCOVERER] Found endpoint: {info.EndpointName}");

        try
        {
            // Complex transformation - use adapter
            var metadata = new Dictionary<string, object>
            {
                ["ServiceId"] = info.ServiceId,
                ["Platform"] = "Android"
            };

            var crossPlatformArgs = new PeerDiscoveredEventArgs(
                peerId: endpointId,
                displayName: info.EndpointName,
                metadata: metadata
            );

            _ = _eventPublisher.PublishPlatformEventAsync(crossPlatformArgs,
                new EventTransformContext(_sessionId, correlationId: "peer_discovery"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DISCOVERER] Error processing found endpoint: {ex}");
        }
    }

    private void OnEndpointLost(string endpointId)
    {
        Console.WriteLine($"[DISCOVERER] Lost endpoint: {endpointId}");

        // Simple case - create event directly
        var deviceLostEvent = new DeviceLostEvent(endpointId, DateTimeOffset.UtcNow);
        _ = _eventPublisher.PublishAsync(deviceLostEvent);
    }

    private void OnDiscoveryFailed(int errorCode)
    {
        // Simple case - create event directly
        var errorEvent = new DiscoveryErrorEvent(
            "Discovery failed",
            errorCode,
            "AndroidNearbyConnections"
        );

        _ = _eventPublisher.PublishAsync(errorEvent);
    }
}
```

## Consumer Usage Examples

### Basic Event Consumption

```csharp
// Subscribe to specific event types
using var session = nearbyConnections.CreateSession();

session.Events
    .OfType<DeviceDiscoveredEvent>()
    .Subscribe(evt => Console.WriteLine($"Found device: {evt.Device.Name}"));

// Filter and process events
session.Events
    .Where(evt => evt.Category == EventCategory.Connection)
    .OfType<ConnectionStateChangedEvent>()
    .Where(evt => evt.CurrentState == ConnectionState.Connected)
    .Subscribe(evt => Console.WriteLine($"Connected to {evt.DeviceId}"));

// Async enumeration pattern (not recommended for UI thread)
await foreach (var evt in session.Events.ToAsyncEnumerable().WithCancellation(cancellationToken))
{
    switch (evt)
    {
        case DeviceDiscoveredEvent discovery:
            await HandleDeviceDiscovered(discovery);
            break;
        case ConnectionStateChangedEvent connection:
            await HandleConnectionChange(connection);
            break;
    }
}
```

### Thread-Safe UI Updates

```csharp
// Background processing with UI thread marshaling
session.Events.Subscribe(async eventItem =>
{
    await Task.Run(() => ProcessEventAsync(eventItem)); // Background processing

    // Marshal back to UI if needed
    MainThread.BeginInvokeOnMainThread(() => UpdateUI(result));
});

// Using reactive schedulers for proper thread management
session.Events
    .ObserveOn(TaskPoolScheduler.Default) // Background processing
    .Select(ProcessEvent)
    .ObserveOn(SynchronizationContext.Current) // UI updates
    .Subscribe(UpdateUI);
```

## Implementation Checklist

- [ ] Create base event infrastructure (`INearbyConnectionsEvent`, `NearbyConnectionsEventBase`, specific event types)
- [ ] Implement event adapter interfaces (`IEventAdapter<,>`, `EventTransformContext`)
- [ ] Create cross-platform event args classes (`PeerDiscoveredEventArgs`, etc.)
- [ ] Build cross-platform adapters (`PeerDiscoveredAdapter`, `ConnectionStateAdapter`)
- [ ] Implement event processing pipeline (`IEventProcessor<>`, `IEventPipeline<>`, `EventPipeline<>`)
- [ ] Create common processors (`CorrelationProcessor`, `DeduplicationProcessor`)
- [ ] Build event publisher (`IEventPublisher`, `EventPublisher`)
- [ ] Update session options to include `EventProviderOptions`
- [ ] Integrate event system into `NearbyConnectionsSession`
- [ ] Update platform-specific discoverers to use the event system
- [ ] Update platform-specific advertisers to use the event system
- [ ] Add comprehensive unit tests for each component
- [ ] Update consumer documentation with usage examples

## Key Benefits

1. **Separation of Concerns**: Platform adaptation, transformation, and processing are separate
2. **Type Safety**: Strong typing throughout with compile-time guarantees
3. **Extensibility**: Easy to add new event types and processing steps
4. **Testability**: Each component can be unit tested in isolation
5. **Performance**: Minimal allocations and efficient event routing
6. **Flexibility**: Support both adapter-based transformation and direct publishing
7. **Error Handling**: Graceful degradation when components fail
8. **Thread Safety**: Safe for use across multiple threads

## Testing Strategy

- Unit test each adapter with mock platform objects
- Unit test processors with synthetic events
- Integration test the complete pipeline with platform callbacks
- Performance test event throughput and memory usage
- Test error conditions and edge cases