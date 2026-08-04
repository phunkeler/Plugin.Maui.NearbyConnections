using System.Collections.Concurrent;
using System.Diagnostics;
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

/// <summary>
/// The app's single payload consumer: reads each connection's inbound stream, turns payloads into
/// <see cref="ChatMessage"/>s, and fans them out as domain messages via <see cref="IMessenger"/>.
/// </summary>
/// <remarks>
/// Fan-out lives here, one layer above the plugin, which is why a per-connection stream is enough:
/// <c>ChatViewModel</c> is an <c>IRecipient&lt;ChatMessageReceived&gt;</c> rather than a second
/// payload consumer. The loop is also why payloads are a stream and not an event — the body awaits
/// video-thumbnail generation, and a <c>void</c>-returning handler cannot express that.
/// </remarks>
public class ChatMessageService : IChatMessageService
{
    readonly IChatMessageRepository _repository;
    readonly IMessenger _messenger;
    readonly IThumbnailService _thumbnailService;

    readonly ConcurrentDictionary<string, NearbyConnection> _connections = [];

    public ChatMessageService(
        INearbySession session,
        IChatMessageRepository repository,
        IMessenger messenger,
        IThumbnailService thumbnailService)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(messenger);
        ArgumentNullException.ThrowIfNull(thumbnailService);

        _repository = repository;
        _messenger = messenger;
        _thumbnailService = thumbnailService;

        // Singleton-lifetime subscriptions: this service must observe every connection for the whole
        // app lifetime, so it never unsubscribes. Page ViewModels must not copy this pattern — see
        // BasePageViewModel.RegisterSessionSubscription.
        session.ConnectionEstablished += OnConnectionEstablished;
        session.ConnectionDropped += OnConnectionDropped;
    }

    void OnConnectionEstablished(object? sender, NearbyConnectionChangedEventArgs e)
    {
        TrackConnection(e.Connection);

        // One consumer per connection, started as the connection opens. The loop ends by itself when
        // the peer disconnects, so it needs no cancellation token and no cleanup.
        _ = ConsumePayloadsAsync(e.Connection);
    }

    void OnConnectionDropped(object? sender, NearbyConnectionChangedEventArgs e)
    {
        _connections.TryRemove(e.Device.Id, out _);
        _repository.ClearSession(e.Device);
    }

    async Task ConsumePayloadsAsync(NearbyConnection connection)
    {
        try
        {
            // Deliberately no token: ReceiveAsync observes cancellation on every iteration, so
            // passing DisconnectedToken would discard payloads buffered just before the drop.
            await foreach (var payload in connection.ReceiveAsync())
            {
                await ProcessPayloadAsync(connection.RemoteDevice, payload);
            }
        }
        catch (Exception ex)
        {
            // Nothing awaits this loop, so an unlogged exception here is invisible.
            Debug.WriteLine($"Payload consumption for {connection.RemoteDevice.Id} ended: {ex}");
        }
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
