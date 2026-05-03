using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class DataReceivedMessage(
    NearbyDevice device,
    DateTimeOffset timestamp,
    NearbyPayload payload) : ValueChangedMessage<NearbyDevice>(device)
{
    public DateTimeOffset Timestamp { get; } = timestamp;
    public NearbyPayload Payload { get; } = payload;
}