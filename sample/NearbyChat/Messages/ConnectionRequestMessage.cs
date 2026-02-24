using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class ConnectionRequestMessage(NearbyDevice device, DateTimeOffset invitedAt)
    : ValueChangedMessage<NearbyDevice>(device)
{
    public DateTimeOffset InvitedAt { get; } = invitedAt;
}
