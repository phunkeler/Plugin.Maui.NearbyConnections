using Microsoft.Data.Sqlite;
using NearbyChat.Models;

namespace NearbyChat.Data;

/// <summary>
/// Repository for managing users in the NearbyChat application.
/// </summary>
public class UserRepository
{
    bool _isInitialized;

    readonly AvatarRepository _avatarRepository;

    public UserRepository(AvatarRepository avatarRepository)
    {
        ArgumentNullException.ThrowIfNull(avatarRepository);

        _avatarRepository = avatarRepository;
    }

    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        await Initialize(cancellationToken);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @$"
            SELECT
                {nameof(User.Id)},
                {nameof(User.DisplayName)},
                {nameof(User.AvatarId)},
                {nameof(User.CreatedOn)}
            FROM User
            LIMIT 1;";

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var user = new User
                {
                    Id = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    AvatarId = reader.GetInt32(2),
                    CreatedOn = reader.GetString(3)
                };

                // Load the associated avatar
                user.Avatar = await _avatarRepository.GetAsync(user.AvatarId, cancellationToken);
                return user;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting current user: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> SaveUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await Initialize(cancellationToken);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var saveCommand = connection.CreateCommand();
        saveCommand.CommandText = @$"
            INSERT OR REPLACE INTO User (
                {nameof(User.Id)},
                {nameof(User.DisplayName)},
                {nameof(User.AvatarId)},
                {nameof(User.CreatedOn)}
            )
            VALUES (
                @{nameof(User.Id)},
                @{nameof(User.DisplayName)},
                @{nameof(User.AvatarId)},
                @{nameof(User.CreatedOn)}
            );";

        saveCommand.Parameters.AddWithValue($"@{nameof(User.Id)}", user.Id);
        saveCommand.Parameters.AddWithValue($"@{nameof(User.DisplayName)}", user.DisplayName);
        saveCommand.Parameters.AddWithValue($"@{nameof(User.AvatarId)}", user.AvatarId);
        saveCommand.Parameters.AddWithValue($"@{nameof(User.CreatedOn)}", user.CreatedOn);

        try
        {
            return await saveCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving user: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await Initialize(cancellationToken);

        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM User WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", userId);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting user: {ex.Message}");
            return false;
        }
    }

    public async Task DropTableAsync(CancellationToken cancellationToken = default)
    {
        await Initialize(cancellationToken);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var dropTableCommand = connection.CreateCommand();
        dropTableCommand.CommandText = $"DROP TABLE IF EXISTS {nameof(User)};";
        await dropTableCommand.ExecuteNonQueryAsync(cancellationToken);

        _isInitialized = false;
    }

    private async Task Initialize(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        try
        {
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @$"
                CREATE TABLE IF NOT EXISTS {nameof(User)} (
                    {nameof(User.Id)} TEXT PRIMARY KEY,
                    {nameof(User.DisplayName)} TEXT NOT NULL,
                    {nameof(User.AvatarId)} INTEGER,
                    {nameof(User.CreatedOn)} TEXT,
                    FOREIGN KEY ({nameof(User.AvatarId)}) REFERENCES {nameof(Avatar)}({nameof(Avatar.Id)})
                );";

            await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing User table: {ex.Message}");
            throw;
        }

        _isInitialized = true;
    }
}
