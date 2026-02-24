using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class DeviceFoundMessage(NearbyDevice device, DateTimeOffset lastSeenAt)
    : ValueChangedMessage<NearbyDevice>(device)
{
    public DateTimeOffset LastSeenAt { get; } = lastSeenAt;
}
