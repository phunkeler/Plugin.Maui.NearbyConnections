using NearbyChat.Models;

namespace NearbyChat.Services;

/// <summary>
/// Service-layer abstraction for managing <see cref="INearbyChatSession"/>
/// </summary>
public interface ISessionService
{
    INearbyChatSession CurrentSession { get; }

    Task StartSession();
}

public class SessionService : ISessionService
{

}