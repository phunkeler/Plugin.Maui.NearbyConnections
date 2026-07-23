using NearbyChat.Models;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.Data;

public interface IChatMessageRepository
{
    IReadOnlyList<ChatMessage> GetAll(NearbyDevice device);
    ChatMessage Save(NearbyDevice device, ChatMessage message);
    void ClearSession(NearbyDevice device);
}
