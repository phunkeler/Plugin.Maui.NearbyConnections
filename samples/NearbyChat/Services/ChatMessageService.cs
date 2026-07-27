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
    Task SendChatMessageAsync(
        NearbyDevice device,
        ChatMessage message,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public class ChatMessageService : IChatMessageService, IAdvertiserHandler, IDiscovererHandler
{
    readonly IChatMessageRepository _repository;
    readonly IMessenger _messenger;
    readonly IThumbnailService _thumbnailService;

    readonly ConcurrentDictionary<string, NearbyConnection> _connections = [];

    IDispatcher? IAdvertiserHandler.Dispatcher => null;
    IDispatcher? IDiscovererHandler.Dispatcher => null;

    public ChatMessageService(
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer,
        IChatMessageRepository repository,
        IMessenger messenger,
        IThumbnailService thumbnailService)
    {
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(messenger);
        ArgumentNullException.ThrowIfNull(thumbnailService);

        _repository = repository;
        _messenger = messenger;
        _thumbnailService = thumbnailService;

        // Singleton-lifetime subscriptions: this service is registered as a singleton and
        // must observe every connection and payload for the whole app lifetime, so it
        // subscribes to both event streams once here and never unsubscribes.
        _ = advertiser.EventsAsync().RunAsync(this);
        _ = discoverer.EventsAsync().RunAsync(this);
    }

    void ProcessIncomingChatMessage(NearbyDevice device, ChatMessage message)
    {
        _repository.Save(device, message);
        _messenger.Send(new ChatMessageReceived(device, message));
    }

    public async Task SendChatMessageAsync(
        NearbyDevice device,
        ChatMessage message,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
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
                await conn.SendAsync(mediaAttachment.FilePath, progress, cancellationToken);
            }
        }
        else if (!string.IsNullOrWhiteSpace(message.Text))
        {
            await conn.SendAsync(Encoding.UTF8.GetBytes(message.Text), cancellationToken);
        }
    }

    Task IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        TrackConnection(ev.Connection);
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        _connections.TryRemove(ev.Connection.RemoteDevice.Id, out _);
        _repository.ClearSession(ev.Connection.RemoteDevice);
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnPayloadReceived(AdvertiserEvent.PayloadReceived ev)
        => ProcessPayloadAsync(ev.Connection.RemoteDevice, ev.Payload);

    Task IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        TrackConnection(ev.Connection);
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        _connections.TryRemove(ev.Connection.RemoteDevice.Id, out _);
        _repository.ClearSession(ev.Connection.RemoteDevice);
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnPayloadReceived(DiscovererEvent.PayloadReceived ev)
        => ProcessPayloadAsync(ev.Connection.RemoteDevice, ev.Payload);

    void TrackConnection(NearbyConnection connection)
    {
        connection.InboundProgress = new InboundProgressRelay(_messenger, connection.RemoteDevice);
        _connections[connection.RemoteDevice.Id] = connection;
    }

    NearbyConnection? FindConnection(NearbyDevice device)
    {
        _connections.TryGetValue(device.Id, out var conn);
        return conn;
    }

    async Task ProcessPayloadAsync(NearbyDevice device, NearbyPayload payload)
    {
        ChatMessage message;

        switch (payload)
        {
            case BytesPayload bytes:
            {
                var text = Encoding.UTF8.GetString(bytes.Data);
                message = new ChatMessage(text, NearbyDirection.Incoming, DateTimeOffset.UtcNow);
                break;
            }

            case FilePayload file:
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
                    var thumbnail = await _thumbnailService.GetVideoThumbnailAsync(path);
                    message.Attachments.Add(new VideoAttachment
                    {
                        FilePath = path,
                        Thumbnail = thumbnail
                    });
                }

                break;
            }

            default:
                return;
        }

        ProcessIncomingChatMessage(device, message);
    }

    sealed class InboundProgressRelay(IMessenger messenger, NearbyDevice device) : IProgress<NearbyTransferProgress>
    {
        public void Report(NearbyTransferProgress value)
            => messenger.Send(new InboundTransferProgress(device, value));
    }
}
