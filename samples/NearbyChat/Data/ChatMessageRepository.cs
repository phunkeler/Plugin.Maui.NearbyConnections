using System.Collections.Concurrent;
using NearbyChat.Models;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.Data;

public class ChatMessageRepository : IChatMessageRepository
{
    // Keyed by device Id — the identity contract the rest of the app uses.
    readonly ConcurrentDictionary<string, List<ChatMessage>> _sessions = [];

    public IReadOnlyList<ChatMessage> GetAll(NearbyDevice device)
    {
        if (!_sessions.TryGetValue(device.Id, out var messages))
        {
            return [];
        }

        // Snapshot under the lock: handler threads mutate the inner list, so a
        // live read-only wrapper over it would race with those mutations.
        lock (messages)
        {
            return [.. messages];
        }
    }

    public ChatMessage Save(NearbyDevice device, ChatMessage message)
    {
        var messages = _sessions.GetOrAdd(device.Id, _ => []);

        lock (messages)
        {
            messages.Add(message);
        }

        return message;
    }

    public void ClearSession(NearbyDevice device)
        => _sessions.TryRemove(device.Id, out _);
}
