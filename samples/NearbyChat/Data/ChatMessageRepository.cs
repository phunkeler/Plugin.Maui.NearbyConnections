using System.Collections.Concurrent;
using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Data;

public class ChatMessageRepository : IChatMessageRepository
{
    readonly ConcurrentDictionary<NearbyDevice, List<ChatMessage>> _sessions = [];

    public IReadOnlyList<ChatMessage> GetAll(NearbyDevice device)
        => _sessions.TryGetValue(device, out var messages)
            ? messages.AsReadOnly()
            : [];

    public ChatMessage Save(NearbyDevice device, ChatMessage message)
    {
        if (!_sessions.TryGetValue(device, out var messages))
        {
            messages = [];
            _sessions.TryAdd(device, messages);
        }

        messages.Add(message);
        return message;
    }

    public void ClearSession(NearbyDevice device)
        => _sessions.TryRemove(device, out _);
}
