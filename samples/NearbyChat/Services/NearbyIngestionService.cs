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
/// <see cref="INearbyDevices.Changes"/> does not replay, so this watcher must be running before
/// the first connection is established. MAUI calls <see cref="Initialize"/> during
/// <c>MauiAppBuilder.Build()</c>, which guarantees that. Resolving <see cref="INearby"/> in the
/// constructor also constructs the plugin's singleton if it is not already alive — DI resolution is
/// idempotent, so it does not matter whether <c>AddNearby</c> or this initializer runs first;
/// whichever asks for it first builds it, and every later resolution (from either) gets that same
/// instance.
/// </para>
/// <para>
/// <strong>Persistence.</strong> This singleton holds the singleton
/// <see cref="ChatMessageStore"/> directly, which is safe only because that store is a thread-safe
/// in-memory dictionary. Backing it with an EF Core <c>DbContext</c> would make it a captive
/// dependency, and this class would then have to resolve one scope per payload instead.
/// </para>
/// </remarks>
public sealed partial class NearbyIngestionService(
    INearby session,
    ChatMessageStore store,
    IMessenger messenger,
    ILogger<NearbyIngestionService> logger) : IMauiInitializeService
{
    readonly INearby _session = session ?? throw new ArgumentNullException(nameof(session));
    readonly ChatMessageStore _store = store ?? throw new ArgumentNullException(nameof(store));
    readonly IMessenger _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
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
    /// performs service location. The watch loop is never cancelled — this singleton lives as long
    /// as the app, which is the whole point.
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

        _ = WatchDevicesAsync();
    }

    /// <summary>
    /// Drives ingestion off the session's change stream: opens a receive loop for every device that
    /// reaches Connected, and clears the chat session for every one that leaves it.
    /// </summary>
    /// <remarks>
    /// The stream reports status transitions, not connection events, so this tracks which devices
    /// it has already started a loop for. Without that, a device that changes any other way while
    /// connected — a display name arriving late, for instance — would start a second consumer for
    /// the same connection and every payload would be processed twice.
    /// </remarks>
    async Task WatchDevicesAsync()
    {
        var consuming = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            await foreach (var change in _session.Devices.Changes)
            {
                var device = change.Device;

                var isConnected = change.Action is not NearbyDeviceChangeAction.Removed
                    && device.Status is NearbyDeviceStatus.Connected;

                if (isConnected)
                {
                    if (consuming.Add(device.Id)
                        && _session.TryGetConnection(device.Id, out var connection))
                    {
                        connection.InboundProgress = new InboundProgressRelay(_messenger, connection.RemoteDevice);

                        // One consumer per connection, started as the connection opens. The loop
                        // ends by itself when the peer disconnects, so it needs no cancellation
                        // token and no cleanup.
                        _ = ConsumePayloadsAsync(connection);
                    }
                }
                else if (consuming.Remove(device.Id))
                {
                    _store.Clear(device.Id);
                }
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

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Device watching ended; no further inbound payloads will be consumed.")]
    partial void LogWatchEnded(Exception exception);

    sealed class InboundProgressRelay(IMessenger messenger, NearbyDevice device) : IProgress<NearbyTransferProgress>
    {
        public void Report(NearbyTransferProgress value)
            => messenger.Send(new InboundTransferProgress(device, value));
    }
}
