namespace NearbyChat.Data;

public static class Constants
{
    public const string DatabaseFileName = "NearbyChat.db3";

    public static string DatabasePath =>
        $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName)}";
}