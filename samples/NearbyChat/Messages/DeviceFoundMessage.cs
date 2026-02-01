using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class DeviceFoundMessage(NearbyDevice device, DateTimeOffset foundAt)
    : ValueChangedMessage<NearbyDevice>(device)
{
    public DateTimeOffset FoundAt { get; } = foundAt;
}
