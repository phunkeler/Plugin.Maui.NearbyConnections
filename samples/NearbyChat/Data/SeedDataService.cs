using System.Text.Json;
using NearbyChat.Models;

namespace NearbyChat.Data;

public interface ISeedDataService
{
    Task LoadSeedDataAsync(CancellationToken cancellationToken = default);
}

public class SeedDataService : ISeedDataService
{
    const string SEED_DATA_FILE_PATH = "SeedData.json";

    readonly AvatarRepository _avatarRepository;

    public SeedDataService(AvatarRepository avatarRepository)
    {
        ArgumentNullException.ThrowIfNull(avatarRepository);

        _avatarRepository = avatarRepository;
    }

    public async Task LoadSeedDataAsync(CancellationToken cancellationToken = default)
    {
        await ClearTables(cancellationToken);

        await using var templateStream = await FileSystem.OpenAppPackageFileAsync(SEED_DATA_FILE_PATH);

        AvatarsJson? avatarsJson = null;

        try
        {
            avatarsJson = await JsonSerializer.DeserializeAsync(
                templateStream,
                JsonContext.Default.AvatarsJson,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error deserializing seed data: {e.Message}");
        }

        try
        {
            if (avatarsJson is not null && avatarsJson.Avatars.Count > 0)
            {
                foreach (var avatar in avatarsJson.Avatars)
                {
                    if (avatar is null)
                    {
                        continue;
                    }

                    await _avatarRepository.SaveAsync(avatar, cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error inserting seed data: {e.Message}");
        }
    }

    private async Task ClearTables(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.WhenAll(
                _avatarRepository.DropTableAsync(cancellationToken));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}