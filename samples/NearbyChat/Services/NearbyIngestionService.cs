using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NearbyChat.Data;
using NearbyChat.Messages;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// The app's single inbound-payload consumer: watches <see cref="INearby.Connections"/>, drains
/// every connection's receive stream, persists each payload as a <see cref="ChatMessage"/>, and
/// fans it out as a domain message.
/// </summary>
/// <remarks>
/// <para>
/// <strong>No startup ritual.</strong> <see cref="INearby.Connections"/> replays the connections
/// still open before following live ones, and an unconsumed connection buffers its payloads until
/// a consumer arrives — so this service can start whenever it is constructed and misses nothing.
/// The app resolves it once at startup (see <c>App</c>), which keeps ingestion running for the
/// life of the app.
/// </para>
/// <para>
/// <strong>Persistence.</strong> This singleton holds the singleton
/// <see cref="ChatMessageStore"/> directly, which is safe only because that store is a thread-safe
/// in-memory dictionary. Backing it with an EF Core <c>DbContext</c> would make it a captive
/// dependency, and this class would then have to resolve one scope per payload instead.
/// </para>
/// </remarks>
public sealed partial class NearbyIngestionService
{
    readonly INearby _nearby;
    readonly ChatMessageStore _store;
    readonly IMessenger _messenger;
    readonly ILogger<NearbyIngestionService> _logger;

    public NearbyIngestionService(
        INearby nearby,
        ChatMessageStore store,
        IMessenger messenger,
        ILogger<NearbyIngestionService> logger)
    {
        _nearby = nearby ?? throw new ArgumentNullException(nameof(nearby));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // The watch loop is never cancelled — this singleton lives as long as the app, which is
        // the whole point.
        _ = WatchConnectionsAsync();
    }

    /// <summary>
    /// The section 2 receive loop: one consumer per connection, started as the stream yields it.
    /// </summary>
    async Task WatchConnectionsAsync()
    {
        try
        {
            await foreach (var connection in _nearby.Connections)
            {
                connection.InboundProgress = new InboundProgressRelay(_messenger, connection.RemoteDevice);

                // One consumer per connection. The receive loop ends by itself when the peer
                // disconnects, so it needs no cancellation token and no cleanup.
                _ = ConsumePayloadsAsync(connection);
            }
        }
        catch (Exception ex)
        {
            // Nothing awaits this loop, so an unlogged exception here would end ingestion silently.
            LogWatchEnded(ex);
        }
    }

    async Task ConsumePayloadsAsync(NearbyConnection connection)
    {
        try
        {
            // Deliberately no token: ReceiveAsync observes cancellation on every iteration, so
            // passing DisconnectedToken would discard payloads buffered just before the drop.
            await foreach (var payload in connection.ReceiveAsync())
            {
                await ProcessPayloadAsync(connection.RemoteDevice, payload).ConfigureAwait(false);
            }

            // A cleanly ended stream is the disconnect: the chat session for this device is over.
            _store.Clear(connection.RemoteDevice.Id);
        }
        catch (Exception ex)
        {
            LogConsumptionEnded(connection.RemoteDevice.Id, ex);
        }
    }

    async Task ProcessPayloadAsync(NearbyDevice device, NearbyPayload payload)
    {
        ChatMessage? message;

        try
        {
            message = await MaterializeAsync(payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogMaterializeFailed(device.Id, ex);
            return;
        }

        if (message is null)
        {
            return;
        }

        try
        {
            _store.Add(device.Id, message);
        }
        catch (Exception ex)
        {
            LogPersistFailed(device.Id, ex);
            return;
        }

        _messenger.Send(new ChatMessageReceived(device, message));
    }

    static async Task<ChatMessage?> MaterializeAsync(NearbyPayload payload)
    {
        switch (payload)
        {
            case NearbyBytesPayload bytes:
                return new ChatMessage(
                    Encoding.UTF8.GetString(bytes.Data),
                    NearbyDirection.Incoming,
                    DateTimeOffset.UtcNow);

            case NearbyFilePayload file:
            {
                var path = file.FileResult.FullPath;
                var contentType = file.FileResult.ContentType ?? string.Empty;

                var message = new ChatMessage(file.FileResult.FileName, NearbyDirection.Incoming, DateTimeOffset.UtcNow);

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
                    var thumbnail = await ThumbnailService.GetVideoThumbnailAsync(path).ConfigureAwait(false);

                    message.Attachments.Add(new VideoAttachment
                    {
                        FilePath = path,
                        Thumbnail = thumbnail
                    });
                }

                return message;
            }

            default:
                return null;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Payload consumption for {DeviceId} ended.")]
    partial void LogConsumptionEnded(string deviceId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to persist an inbound message from {DeviceId}.")]
    partial void LogPersistFailed(string deviceId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to materialize an inbound payload from {DeviceId}; it was dropped.")]
    partial void LogMaterializeFailed(string deviceId, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Connection watching ended; no further inbound payloads will be consumed.")]
    partial void LogWatchEnded(Exception exception);

    sealed class InboundProgressRelay(IMessenger messenger, NearbyDevice device) : IProgress<NearbyTransferProgress>
    {
        public void Report(NearbyTransferProgress value)
            => messenger.Send(new InboundTransferProgress(device, value));
    }
}
