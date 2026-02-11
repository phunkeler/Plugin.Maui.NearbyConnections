namespace NearbyChat.Models;

public enum Sender
{
    Me,
    Peer
}

public class ChatMessage
{
    public required string Text { get; set; }
    public required Sender From { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}