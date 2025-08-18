using Indiko.Maui.Controls.Chat.Models;

namespace NearbyChat.Services;

public interface IChatMessageService
{
    Task<IEnumerable<ChatMessage>> GetMessagesAsync(DateTime? from, DateTime? until);
}

public class ChatMessageService : IChatMessageService
{
    public Task<IEnumerable<ChatMessage>> GetMessagesAsync(DateTime? from, DateTime? until)
    {
        // Implementation to fetch messages from a data source
        // This could be a local database, remote API, etc.`
        // For now, returning an empty list as a placeholder
        return Task.FromResult<IEnumerable<ChatMessage>>([]);
    }
}
