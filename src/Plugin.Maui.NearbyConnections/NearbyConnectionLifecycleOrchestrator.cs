namespace Plugin.Maui.NearbyConnections;

internal class NearbyConnectionLifecycleOrchestrator : INearbyConnectionLifecycleOrchestrator
{
    private readonly DefaultNearbyConnectionsEventProcessor _defaultProcessor;
    private readonly List<INearbyConnectionsEventProcessor> _consumerProcessors = new();

    public NearbyConnectionLifecycleOrchestrator()
    {
        // Single default processor handles all internal library behavior
        _defaultProcessor = new DefaultNearbyConnectionsEventProcessor(this);
    }

    public async Task ProcessEventAsync<T>(T nearbyEvent, CancellationToken cancellationToken = default)
        where T : INearbyConnectionsEvent
    {
        // 1. Always process through default processor first (internal library behavior)
        var processedEvent = _defaultProcessor.ProcessEvent(nearbyEvent);

        if (processedEvent == null)
            return; // Default processor handled/consumed the event

        // 2. Chain through consumer processors (enhanced/custom behavior)
        var currentEvent = processedEvent;
        foreach (var processor in _consumerProcessors)
        {
            currentEvent = processor.ProcessEvent(currentEvent);
            if (currentEvent == null)
                return; // Consumer processor consumed the event
        }

        // 3. Emit final events to consumers after processing chain
        await EmitConsumerEvents(currentEvent);
    }

    // Consumer processor chain management
    public void AddProcessor(INearbyConnectionsEventProcessor processor)
    {
        _consumerProcessors.Add(processor);
    }

    public void RemoveProcessor(INearbyConnectionsEventProcessor processor)
    {
        _consumerProcessors.Remove(processor);
    }

    public void ClearProcessors()
    {
        _consumerProcessors.Clear();
    }

    private async Task EmitConsumerEvents(INearbyConnectionsEvent processedEvent)
    {
        switch (processedEvent)
        {
            case InvitationReceived invitation:
                InvitationReceived?.Invoke(this, new InvitationReceivedEventArgs
                {
                    InvitingPeer = invitation.InvitingPeer,
                    ConnectionEndpoint = invitation.ConnectionEndpoint
                });
                break;

            case PeerDiscovered discovery:
                PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs
                {
                    Peer = discovery.DiscoveredPeer
                });
                break;

            case MessageReceived message:
                MessageReceived?.Invoke(this, new PeerMessageReceivedEventArgs
                {
                    Message = message.MessageData
                });
                break;
        }
    }

    // Events
    public event EventHandler<InvitationReceivedEventArgs>? InvitationReceived;
    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
    public event EventHandler<ConnectionEstablishedEventArgs>? ConnectionEstablished;
    public event EventHandler<PeerMessageReceivedEventArgs>? MessageReceived;
}