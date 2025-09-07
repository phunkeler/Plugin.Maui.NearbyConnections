namespace Plugin.Maui.NearbyConnections.Events.Pipeline;

/// <summary>
/// Orchestrates multiple processors in a pipeline.
/// </summary>
public interface INearbyConnectionsEventPipeline<TEvent>
    where TEvent : INearbyConnectionsEvent
{
    /// <summary>
    /// Process an event through all registered processors.
    /// </summary>
    TEvent? Process(TEvent eventItem);

    /// <summary>
    /// Add a processor to the pipeline.
    /// </summary>
    INearbyConnectionsEventPipeline<TEvent> AddProcessor(INearbyConnectionsEventProcessor<TEvent> processor);
}

/// <summary>
/// Default pipeline implementation that processes events sequentially.
/// </summary>
public sealed class NearbyConnectionsEventPipeline<TEvent> : INearbyConnectionsEventPipeline<TEvent>
    where TEvent : INearbyConnectionsEvent
{
    readonly List<INearbyConnectionsEventProcessor<TEvent>> _processors = [];

    public TEvent? Process(TEvent eventItem)
    {
        var current = eventItem;

        foreach (var processor in _processors)
        {
            if (current is null)
            {
                break;
            }

            try
            {
                current = processor.Process(current);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pipeline processor error: {ex}");
            }
        }

        return current;
    }

    public INearbyConnectionsEventPipeline<TEvent> AddProcessor(INearbyConnectionsEventProcessor<TEvent> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        _processors.Add(processor);
        return this;
    }
}