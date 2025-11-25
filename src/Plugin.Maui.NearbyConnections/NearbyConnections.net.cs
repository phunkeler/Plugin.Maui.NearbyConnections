namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnections : INearbyConnections
{
    Task PlatformSendInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    Task PlatformAcceptInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    Task PlatformDeclineInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

}