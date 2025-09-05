namespace NearbyChat.Models;

/// <summary>
/// A transient conversation between <see cref="User"/>s.
/// </summary>
public interface INearbyChatSession
{
    /// <summary>
    /// Gets the user that initiated this session.
    /// </summary>
    User CreatedBy { get; }

    /// <summary>
    /// Gets the point in time when this session was created.
    /// </summary>
    DateTimeOffset CreatedOn { get; }

}

/// <summary>
/// The session.
/// </summary>
public class NearbyChatSession(User createdBy, DateTimeOffset createdOn) : INearbyChatSession
{
    /// <inheritdoc/>
    public User CreatedBy { get; } = createdBy;

    /// <inheritdoc/>
    public DateTimeOffset CreatedOn { get; } = createdOn;
}