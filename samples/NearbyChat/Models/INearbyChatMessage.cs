namespace NearbyChat.Models;

public interface INearbyChatMessage
{
    DateTimeOffset Timestamp { get; }
    User Sender { get; }
}

public class NearbyChatMessage : INearbyChatMessage
{
    public DateTimeOffset Timestamp => throw new NotImplementedException();

    public User Sender => throw new NotImplementedException();
}

public interface INearbyChatMessageContent
{
    // We need a way of embedding links
}