using NearbyChat.Data;
using NearbyChat.Models;

namespace NearbyChat.Services;

public interface IUserService
{
    Task<User?> GetActiveUserAsync(CancellationToken cancellationToken);
}

public class UserService : IUserService
{
    readonly UserRepository _userRepository;

    public UserService(
        UserRepository userRepo)
    {
        ArgumentNullException.ThrowIfNull(userRepo);

        _userRepository = userRepo;
    }

    public Task<User?> GetActiveUserAsync(CancellationToken cancellationToken = default)
        => _userRepository.GetActiveUserAsync(cancellationToken);
}