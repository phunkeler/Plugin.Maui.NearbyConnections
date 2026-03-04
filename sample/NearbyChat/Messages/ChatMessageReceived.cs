using CommunityToolkit.Mvvm.Messaging.Messages;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class ChatMessageReceived(
    NearbyDevice device,
    ChatMessage chatMessage) : ValueChangedMessage<NearbyDevice>(device)
{
    public ChatMessage ChatMessage { get; } = chatMessage;
}