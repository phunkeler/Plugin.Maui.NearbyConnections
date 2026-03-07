using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class DeviceFoundMessage(NearbyDevice device, DateTimeOffset lastSeen)
    : ValueChangedMessage<NearbyDevice>(device)
{
    public DateTimeOffset LastSeen { get; } = lastSeen;
}
