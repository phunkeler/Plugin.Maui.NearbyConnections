using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class ConnectionResponseMessage(NearbyDevice device, bool accepted)
    : ValueChangedMessage<NearbyDevice>(device)
{
    public bool Accepted { get; } = accepted;
}
