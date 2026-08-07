using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NearbyChat.Data;
using NearbyChat.Messages;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// The app's single inbound-payload consumer: drains every connection's receive stream, persists
/// each payload as a <see cref="ChatMessage"/>, and fans it out as a domain message.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this implements <see cref="IMauiInitializeService"/>.</strong>
/// <see cref="INearbySession.ConnectionEstablished"/> is a plain event with no replay, so this
/// subscriber must be attached before the first connection is established. MAUI calls
/// <see cref="Initialize"/> during <c>MauiAppBuilder.Build()</c>, which guarantees that.
/// (<c>AddNearbyConnections</c> uses the same hook to construct the session itself, so the session
/// exists by the time this runs.)
/// </para>
/// <para>
/// <strong>Separation of concerns.</strong> Ingestion (this class) is deliberately split from the
/// send/query surface (<see cref="IChatMessageService"/>). Only ingestion has to exist at startup;
/// keeping them apart means the send path stays an ordinary lazily-resolved service and no consumer
/// carries a load-bearing dependency it does not otherwise use.
/// </para>
/// <para>
/// <strong>Scoped persistence.</strong> This is a singleton, so it must never hold an
/// <see cref="IChatMessageRepository"/>. It opens one unit of work per payload through
/// <see cref="IChatMessageRepositoryFactory"/> — the lifetime an EF Core <c>DbContext</c> requires.
/// Awaiting that write inside the loop is safe by design: <c>ReceiveAsync</c> does not dequeue the
/// next payload until the body completes, so persistence backpressures the stream instead of racing
/// it.
/// </para>
/// </remarks>
public sealed partial class NearbyIngestionService(
    INearbySession session,
    IChatMessageRepositoryFactory repositoryFactory,
    IMessenger messenger,
    IThumbnailService thumbnailService,
    ILogger<NearbyIngestionService> logger) : IMauiInitializeService
{
    readonly INearbySession _session = session ?? throw new ArgumentNullException(nameof(session));
    readonly IChatMessageRepositoryFactory _repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
    readonly IMessenger _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    readonly IThumbnailService _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
    readonly ILogger<NearbyIngestionService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    int _initialized;

    /// <summary>
    /// Attaches the session subscriptions. Called once by MAUI during <c>Build()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This runs inside <c>MauiAppBuilder.Build()</c>, before <c>Application.Current</c> exists, so
    /// everything it touches must be resolvable that early. In particular, do not register
    /// <c>IDispatcher</c> with a factory that reads <c>Application.Current</c> — MAUI registers a
    /// perfectly good one, and an override like <c>Application.Current?.Dispatcher ?? throw</c>
    /// turns startup resolution into a crash.
    /// </para>
    /// <para>
    /// The <paramref name="services"/> parameter is part of the framework contract and is
    /// deliberately unused: every dependency arrives by constructor injection, so this type never
    /// performs service location. Subscriptions are never detached — this singleton lives as long as
    /// the app, which is the whole point. Page ViewModels must not copy that; see
    /// <c>BasePageViewModel.RegisterSessionSubscription</c>.
    /// </para>
    /// </remarks>
    public void Initialize(IServiceProvider services)
    {
        // MAUI invokes IMauiInitializeService registrations via GetServices<T>(), so a duplicate
        // registration would subscribe twice and deliver every payload twice.
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        _session.ConnectionEstablished += OnConnectionEstablished;
        _session.ConnectionDropped += OnConnectionDropped;
    }

    void OnConnectionEstablished(object? sender, NearbyConnectionChangedEventArgs e)
    {
        e.Connection.InboundProgress = new InboundProgressRelay(_messenger, e.Connection.RemoteDevice);

        // One consumer per connection, started as the connection opens. The loop ends by itself when
        // the peer disconnects, so it needs no cancellation token and no cleanup.
        _ = ConsumePayloadsAsync(e.Connection);
    }

    void OnConnectionDropped(object? sender, NearbyConnectionChangedEventArgs e)
        => _ = ClearSessionAsync(e.Device);

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
        }
        catch (Exception ex)
        {
            // Nothing awaits this loop, so an unlogged exception here is invisible.
            LogConsumptionEnded(connection.RemoteDevice.Id, ex);
        }
    }

    async Task ProcessPayloadAsync(NearbyDevice device, NearbyPayload payload)
    {
        var message = await MaterializeAsync(payload).ConfigureAwait(false);

        if (message is null)
        {
            return;
        }

        try
        {
            // One unit of work per payload. Awaited inside the loop, so the next payload is not
            // dequeued until this one is durably stored.
            await using var handle = _repositoryFactory.Create();
            await handle.Repository.SaveAsync(device, message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A failed write must not tear down the loop and lose every subsequent payload.
            LogPersistFailed(device.Id, ex);
            return;
        }

        _messenger.Send(new ChatMessageReceived(device, message));
    }

    async Task<ChatMessage?> MaterializeAsync(NearbyPayload payload)
    {
        switch (payload)
        {
            case BytesPayload bytes:
                return new ChatMessage(
                    Encoding.UTF8.GetString(bytes.Data),
                    NearbyDirection.Incoming,
                    DateTimeOffset.UtcNow);

            case FilePayload file:
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
                    // The await this loop exists for: thumbnail generation is real work, and the
                    // stream holds the next payload until it finishes.
                    var thumbnail = await _thumbnailService.GetVideoThumbnailAsync(path).ConfigureAwait(false);

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

    async Task ClearSessionAsync(NearbyDevice device)
    {
        try
        {
            await using var handle = _repositoryFactory.Create();
            await handle.Repository.ClearSessionAsync(device).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogClearSessionFailed(device.Id, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Payload consumption for {DeviceId} ended.")]
    partial void LogConsumptionEnded(string deviceId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to persist an inbound message from {DeviceId}.")]
    partial void LogPersistFailed(string deviceId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to clear the chat session for {DeviceId}.")]
    partial void LogClearSessionFailed(string deviceId, Exception exception);

    sealed class InboundProgressRelay(IMessenger messenger, NearbyDevice device) : IProgress<NearbyTransferProgress>
    {
        public void Report(NearbyTransferProgress value)
            => messenger.Send(new InboundTransferProgress(device, value));
    }
}
