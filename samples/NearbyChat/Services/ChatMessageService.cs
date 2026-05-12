using System.Collections.Concurrent;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Data;
using NearbyChat.Messages;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public interface IChatMessageService
{
    void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message);
    Task SendChatMessage(NearbyDevice device, ChatMessage message);
}

public class ChatMessageService : IChatMessageService, IAdvertiserHandler, IDiscovererHandler
{
    readonly IChatMessageRepository _repository;
    readonly IMessenger _messenger;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDiscoverer _discoverer;

    ConcurrentDictionary<string, NearbyConnection> _connections = [];

    IDispatcher? IAdvertiserHandler.Dispatcher => null;
    IDispatcher? IDiscovererHandler.Dispatcher => null;

    public ChatMessageService(
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer,
        IChatMessageRepository repository,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(messenger);

        _advertiser = advertiser;
        _discoverer = discoverer;
        _repository = repository;
        _messenger = messenger;

        _ = advertiser.EventsAsync().RunAsync(this);
        _ = discoverer.EventsAsync().RunAsync(this);
    }

    public void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message)
    {
        _repository.Save(device, message);
        _messenger.Send(new ChatMessageReceived(device, message));
    }

    public async Task SendChatMessage(NearbyDevice device, ChatMessage message)
    {
        _repository.Save(device, message);

        var conn = FindConnection(device);
        if (conn is null)
        {
            return;
        }

        if (message.Attachments.FirstOrDefault() is MediaAttachment mediaAttachment)
        {
            if (!string.IsNullOrWhiteSpace(mediaAttachment.FilePath))
            {
                await conn.SendAsync(mediaAttachment.FilePath);
            }
        }
        else if (!string.IsNullOrWhiteSpace(message.Text))
        {
            await conn.SendAsync(Encoding.UTF8.GetBytes(message.Text));
        }
    }

    void IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        _connections[ev.Connection.RemoteDevice.Id] = ev.Connection;
    }

    void IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        _connections.TryRemove(ev.Connection.RemoteDevice.Id, out _);
        _repository.ClearSession(ev.Connection.RemoteDevice);
    }

    void IAdvertiserHandler.OnPayloadReceived(AdvertiserEvent.PayloadReceived ev)
    {
        ProcessPayload(ev.Connection.RemoteDevice, ev.Payload);
    }

    void IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        _connections[ev.Connection.RemoteDevice.Id] = ev.Connection;
    }

    void IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        _connections.TryRemove(ev.Connection.RemoteDevice.Id, out _);
        _repository.ClearSession(ev.Connection.RemoteDevice);
    }

    void IDiscovererHandler.OnPayloadReceived(DiscovererEvent.PayloadReceived ev)
    {
        ProcessPayload(ev.Connection.RemoteDevice, ev.Payload);
    }

    NearbyConnection? FindConnection(NearbyDevice device)
    {
        _connections.TryGetValue(device.Id, out var conn);
        return conn;
    }

    void ProcessPayload(NearbyDevice device, NearbyPayload payload)
    {
        ChatMessage message;

        if (payload is BytesPayload bytes)
        {
            var text = Encoding.UTF8.GetString(bytes.Data);
            message = new ChatMessage(text, NearbyDirection.Incoming, DateTimeOffset.UtcNow);
        }
        else if (payload is FilePayload file)
        {
            var path = file.FileResult.FullPath;
            var contentType = file.FileResult.ContentType ?? string.Empty;

            message = new ChatMessage(file.FileResult.FileName, NearbyDirection.Incoming, DateTimeOffset.UtcNow);

            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                message.Attachments.Add(new PhotoAttachment
                {
                    FilePath = path,
                    Thumbnail = ImageSource.FromFile(path)
                });
            }
            else if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                message.Attachments.Add(new VideoAttachment { FilePath = path });
            }
        }
        else
        {
            return;
        }

        ProcessIncomingChatMessage(device, message);
    }
}
