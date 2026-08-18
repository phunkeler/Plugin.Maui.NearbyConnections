using System.Collections.Concurrent;
using NearbyChat.Models;

namespace NearbyChat.Data;

/// <summary>
/// The process-lifetime store for chat history, keyed by device id.
/// </summary>
/// <remarks>
/// A thread-safe in-memory dictionary, standing in for the database a real app would use. A
/// database-backed store is inherently async and an EF Core <c>DbContext</c> is a unit of work
/// that must not be shared, so replacing this means an async interface registered <c>Scoped</c>,
/// with one scope resolved per operation.
/// </remarks>
public sealed class ChatMessageStore
{
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
