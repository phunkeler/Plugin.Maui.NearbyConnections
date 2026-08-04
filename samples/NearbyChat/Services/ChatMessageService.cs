using System.Text;
using NearbyChat.Data;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// The outbound half of chat: sends a message to a connected device and persists it locally.
/// </summary>
public interface IChatMessageService
{
    Task SendChatMessageAsync(
        NearbyDevice device,
        ChatMessage message,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        NearbyDevice device,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Send and query only. Inbound payload consumption lives in <see cref="NearbyIngestionService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This service has no lifetime significance, by design.</strong> It subscribes to nothing
/// and starts nothing, so it is safe to resolve lazily, whenever a ViewModel first needs it. That is
/// the point of splitting it from <see cref="NearbyIngestionService"/>: previously a single class
/// owned both jobs, which meant the receive loops only started if something happened to resolve the
/// service early enough — and nothing did until the chat sheet opened, by which time the
/// <c>ConnectionEstablished</c> event had already fired and inbound messages were silently lost.
/// </para>
/// <para>
/// The connection is read from <see cref="NearbyDevice.Connection"/> rather than tracked in a
/// dictionary here: the session already owns that state and clears it on drop, so mirroring it would
/// be a second source of truth that can go stale.
/// </para>
/// </remarks>
public sealed class ChatMessageService(IChatMessageRepositoryFactory repositoryFactory) : IChatMessageService
{
    readonly IChatMessageRepositoryFactory _repositoryFactory = repositoryFactory
        ?? throw new ArgumentNullException(nameof(repositoryFactory));

    public async Task SendChatMessageAsync(
        NearbyDevice device,
        ChatMessage message,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(message);

        await using (var handle = _repositoryFactory.Create())
        {
            await handle.Repository.SaveAsync(device, message, cancellationToken).ConfigureAwait(false);
        }

        if (device.Connection is not { } connection)
        {
            return;
        }

        if (message.Attachments.FirstOrDefault() is MediaAttachment { FilePath: { Length: > 0 } filePath })
        {
            await connection.SendAsync(filePath, progress, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(message.Text))
        {
            await connection.SendAsync(Encoding.UTF8.GetBytes(message.Text), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(
        NearbyDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        await using var handle = _repositoryFactory.Create();

        return await handle.Repository.GetAllAsync(device, cancellationToken).ConfigureAwait(false);
    }
}
