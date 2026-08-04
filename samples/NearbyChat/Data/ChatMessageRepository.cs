using System.Collections.Concurrent;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Data;

/// <summary>
/// The process-lifetime store behind <see cref="ChatMessageRepository"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton and injected into the scoped repository, this plays the role a database
/// plays in a real app: the durable thing that outlives any one unit of work. Without it the store
/// would live in the repository itself, and making the repository scoped would silently discard
/// history the moment a scope ended.
/// </remarks>
public sealed class ChatMessageStore
{
    // Keyed by device Id — the identity contract the rest of the app uses.
    readonly ConcurrentDictionary<string, List<ChatMessage>> _sessions = [];

    public IReadOnlyList<ChatMessage> GetAll(string deviceId)
    {
        if (!_sessions.TryGetValue(deviceId, out var messages))
        {
            return [];
        }

        // Snapshot under the lock: ingestion threads mutate the inner list, so a live read-only
        // wrapper over it would race with those mutations.
        lock (messages)
        {
            return [.. messages];
        }
    }

    public void Add(string deviceId, ChatMessage message)
    {
        var messages = _sessions.GetOrAdd(deviceId, _ => []);

        lock (messages)
        {
            messages.Add(message);
        }
    }

    public void Clear(string deviceId) => _sessions.TryRemove(deviceId, out _);
}

/// <summary>
/// In-memory <see cref="IChatMessageRepository"/>, standing in for a database-backed implementation.
/// </summary>
/// <remarks>
/// Registered <c>Scoped</c> and resolved through <see cref="IChatMessageRepositoryFactory"/>, so it
/// occupies exactly the position an EF Core repository would: created per unit of work, disposed
/// with its scope. The methods complete synchronously and return completed tasks — the async
/// signature exists for the implementations that replace this one, not for this one's benefit.
/// </remarks>
public sealed class ChatMessageRepository(ChatMessageStore store) : IChatMessageRepository
{
    readonly ChatMessageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<ChatMessage>> GetAllAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_store.GetAll(device.Id));
    }

    public Task<ChatMessage> SaveAsync(NearbyDevice device, ChatMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        _store.Add(device.Id, message);

        return Task.FromResult(message);
    }

    public Task ClearSessionAsync(NearbyDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();

        _store.Clear(device.Id);

        return Task.CompletedTask;
    }
}
