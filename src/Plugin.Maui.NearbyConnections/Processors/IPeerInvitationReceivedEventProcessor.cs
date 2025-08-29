using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections.Processors;

/// <summary>
/// Processor interface for handling peer invitation received events.
/// Implementations should handle the invitation response logic (accept/reject).
/// </summary>
public interface IPeerInvitationReceivedEventProcessor : INearbyConnectionsEventProcessor
{
    /// <summary>
    /// Processes a peer invitation received event.
    /// </summary>
    /// <param name="invitationEvent">The invitation event to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task that completes when the invitation has been processed.</returns>
    Task ProcessInvitationAsync(InvitationReceived invitationEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a peer invitation.
    /// </summary>
    /// <param name="connectionEndpoint">The connection endpoint to accept.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task that completes when the invitation has been accepted.</returns>
    Task AcceptInvitationAsync(string connectionEndpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a peer invitation.
    /// </summary>
    /// <param name="connectionEndpoint">The connection endpoint to reject.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task that completes when the invitation has been rejected.</returns>
    Task RejectInvitationAsync(string connectionEndpoint, CancellationToken cancellationToken = default);
}