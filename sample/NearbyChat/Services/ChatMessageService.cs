using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Data;
using NearbyChat.Messages;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface IChatMessageService :
    IRecipient<DeviceDisconnectedMessage>
{
    void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message);
    Task SendChatMessage(NearbyDevice device, ChatMessage message);
}

public class ChatMessageService(
    INearbyConnectionsService nearbyConnectionsService,
    IChatMessageRepository repository, IMessenger messenger) : IChatMessageService
{
    public void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message)
    {
        repository.Save(device, message);
        messenger.Send(new ChatMessageReceived(device, message));
    }

    public async Task SendChatMessage(NearbyDevice device, ChatMessage message)
    {
        repository.Save(device, message);

        if (message.Attachments.FirstOrDefault() is MediaAttachment mediaAttachment)
        {
            if (!string.IsNullOrWhiteSpace(mediaAttachment.FilePath))
            {
                await nearbyConnectionsService.SendAsync(device, mediaAttachment.FilePath);
            }
        }
        else if (!string.IsNullOrWhiteSpace(message.Text))
        {
            await nearbyConnectionsService.SendMessage(device, message.Text);
        }
    }

    public void Receive(DeviceDisconnectedMessage message)
        => repository.ClearSession(message.Value);
}