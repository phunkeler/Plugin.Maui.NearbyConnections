using System.Reactive.Linq;
using System.Reactive.Subjects;
using Plugin.Maui.NearbyConnections.Events.Pipeline;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// Internal event aggregator.
/// </summary>
public interface INearbyConnectionsEventPublisher : IDisposable
{
    /// <summary>
    /// Gets an <see cref="System.IObservable{T}"/>.
    /// </summary>
    IObservable<INearbyConnectionsEvent> Events { get; }

    /// <summary>
    /// Publish a plugin event.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="eventItem"></param>
    void Publish<TEvent>(TEvent eventItem) where TEvent : INearbyConnectionsEvent;

    /// <summary>
    /// Publish a platform-specific event to be transformed by an adapter.
    /// </summary>
    /// <typeparam name="TPlatformArgs"></typeparam>
    /// <param name="platformArgs"></param>
    void PublishPlatformEvent<TPlatformArgs>(TPlatformArgs platformArgs);

    /// <summary>
    /// Register a transformer.
    /// </summary>
    /// <typeparam name="TPlatformArgs"></typeparam>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="adapter"></param>
    void RegisterAdapter<TPlatformArgs, TEvent>(IEventAdapter<TPlatformArgs, TEvent> adapter) where TEvent : INearbyConnectionsEvent;
}

/// <summary>
/// Thread-safe event publisher implementation using Reactive Extensions.
/// </summary>
internal sealed class NearbyConnectionsEventPublisher(INearbyConnectionsEventPipeline<INearbyConnectionsEvent> pipeline) : INearbyConnectionsEventPublisher
{
    readonly Subject<INearbyConnectionsEvent> _eventSubject = new();
    readonly INearbyConnectionsEventPipeline<INearbyConnectionsEvent> _pipeline = pipeline;
    readonly Dictionary<Type, object> _adapters = [];
    volatile bool _disposed;

    public IObservable<INearbyConnectionsEvent> Events => _eventSubject.AsObservable();

    // Direct event publishing (bypasses adapters)
    public void Publish<TEvent>(TEvent eventItem) where TEvent : INearbyConnectionsEvent
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var processedEvent = _pipeline.Process(eventItem);

            if (processedEvent is not null)
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
    public void PublishPlatformEvent<TPlatformArgs>(TPlatformArgs platformArgs)
    {
        if (_disposed || !_adapters.TryGetValue(typeof(TPlatformArgs), out var adapter))
        {
            return;
        }

        if (adapter is IEventAdapter<TPlatformArgs, INearbyConnectionsEvent> typedAdapter)
        {
            var crossPlatformEvent = typedAdapter.Transform(platformArgs);

            if (crossPlatformEvent is not null)
            {
                Publish(crossPlatformEvent);
            }
        }
    }

    public void RegisterAdapter<TPlatformArgs, TEvent>(IEventAdapter<TPlatformArgs, TEvent> adapter)
        where TEvent : INearbyConnectionsEvent
    {
        _adapters[typeof(TPlatformArgs)] = adapter;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _eventSubject.OnCompleted();
        _eventSubject.Dispose();
    }
}