using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Processors;

namespace Plugin.Maui.NearbyConnections;

// Updated orchestrator interface - single processor chain
public interface INearbyConnectionLifecycleOrchestrator
{
    // Phase coordination
    Task<NearbyConnectionResult> EstablishConnectionAsync(string peerId, CancellationToken cancellationToken);

    // Event processing delegation - single entry point
    Task ProcessEventAsync<T>(T nearbyEvent, CancellationToken cancellationToken)
        where T : INearbyConnectionsEvent;

    // Consumer processor chain control
    void AddEventProcessor(INearbyConnectionEventProcessor processor);

    // State management
    ConnectionPhase GetConnectionPhase(string peerId);
    IReadOnlyList<PeerDevice> GetPeersByPhase(ConnectionPhase phase);
    IReadOnlyList<ConnectionAttempt> GetActiveConnectionAttempts();

    // Session management
    Task<INearbyConnection> StartSessionAsync(NearbyConnectionOptions options, CancellationToken cancellationToken);
    Task StopSessionAsync(CancellationToken cancellationToken);

    // Events (emitted after processing chain)
    event EventHandler<InvitationReceivedEventArgs> InvitationReceived;
    event EventHandler<PeerDiscoveredEventArgs> PeerDiscovered;
    event EventHandler<ConnectionEstablishedEventArgs> ConnectionEstablished;
    event EventHandler<PeerMessageReceivedEventArgs> MessageReceived;
}