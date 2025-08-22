using System.Globalization;
using Microsoft.Data.Sqlite;
using NearbyChat.Models;

namespace NearbyChat.Data;

/// <summary>
/// Repository for managing avatars in the NearbyChat application.
/// </summary>
public class AvatarRepository
{
    bool _isInitialized;

    public async Task<List<Avatar>> ListAsync(CancellationToken cancellationToken = default)
    {
        await Initialize(cancellationToken);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = $"SELECT * FROM {nameof(Avatar)}";
        var avatars = new List<Avatar>();

        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            Console.WriteLine($"Avatar Id: {reader.GetInt32(0)}");
            Console.WriteLine($"Avatar BackgroundColor: {reader.GetString(1)}");
            Console.WriteLine($"Avatar BorderColor: {reader.GetString(2)}");
            Console.WriteLine($"Avatar BorderWidth: {reader.GetDouble(3)}");
            Console.WriteLine($"Avatar Padding: {reader.GetInt32(5)}");
            Console.WriteLine($"Avatar Text: {reader.GetString(6)}");
            Console.WriteLine($"Avatar TextColor: {reader.GetString(7)}");


            avatars.Add(new Avatar
            {
                Id = reader.GetInt32(0),
                BackgroundColor = reader.GetString(1),
                BorderColor = reader.GetString(2),
                BorderWidth = reader.GetDouble(3),
                ImageSource = reader.IsDBNull(4) ? [] : (byte[])reader.GetValue(4),
                Padding = reader.GetInt32(5),
                Text = reader.GetString(6),
                TextColor = reader.GetString(7)
            });
        }

        return avatars;
    }

    /// <summary>
	/// Drops the Tag and ProjectsTags tables from the database.
	/// </summary>
	public async Task DropTableAsync(CancellationToken cancellationToken = default)
    {
        await Initialize(cancellationToken);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var dropTableCommand = connection.CreateCommand();
        dropTableCommand.CommandText = $"DROP TABLE IF EXISTS {nameof(Avatar)}";
        await dropTableCommand.ExecuteNonQueryAsync(cancellationToken);

        _isInitialized = false;
    }

    /// <summary>
	/// Saves an <see cref="Avatar"/> to the database. If it's Id is 0, a new record is created; otherwise, the existing record is updated.
	/// </summary>
	/// <param name="avatar">The <see cref="Avatar"/> to save.</param>
	/// <returns>The Id of the saved avatar.</returns>
	public async Task<int> SaveItemAsync(Avatar avatar, CancellationToken cancellationToken = default)
    {
        await Initialize(cancellationToken);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);

        var saveCmd = connection.CreateCommand();

        if (avatar.Id == 0)
        {
            saveCmd.CommandText = @$"
                INSERT INTO {nameof(Avatar)} (BackgroundColor, BorderColor, BorderWidth, ImageSource, Padding, Text, TextColor)
                VALUES (@BackgroundColor, @BorderColor, @BorderWidth, @ImageSource, @Padding, @Text, @TextColor);
                SELECT last_insert_rowid();";
        }
        else
        {
            saveCmd.CommandText = @$"
                UPDATE {nameof(Avatar)} SET
                    BackgroundColor = @BackgroundColor,
                    BorderColor = @BorderColor,
                    BorderWidth = @BorderWidth,
                    ImageSource = @ImageSource,
                    Padding = @Padding,
                    Text = @Text,
                    TextColor = @TextColor
                WHERE Id = @ID;";
            saveCmd.Parameters.AddWithValue("@ID", avatar.Id);
        }

        saveCmd.Parameters.AddWithValue("@BackgroundColor", avatar.BackgroundColor);
        saveCmd.Parameters.AddWithValue("@BorderColor", avatar.BorderColor);
        saveCmd.Parameters.AddWithValue("@BorderWidth", avatar.BorderWidth);
        saveCmd.Parameters.AddWithValue("@ImageSource", avatar.ImageSource);
        saveCmd.Parameters.AddWithValue("@Padding", avatar.Padding);
        saveCmd.Parameters.AddWithValue("@Text", avatar.Text);
        saveCmd.Parameters.AddWithValue("@TextColor", avatar.TextColor);

        var result = await saveCmd.ExecuteScalarAsync(cancellationToken);

        if (avatar.Id == 0)
        {
            avatar.Id = Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        return avatar.Id;
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
                CREATE TABLE IF NOT EXISTS {nameof(Avatar)} (
                    {nameof(Avatar.Id)} INTEGER PRIMARY KEY AUTOINCREMENT,
                    {nameof(Avatar.BackgroundColor)} TEXT,
                    {nameof(Avatar.BorderColor)} TEXT,
                    {nameof(Avatar.BorderWidth)} REAL,
                    {nameof(Avatar.ImageSource)} BLOB,
                    {nameof(Avatar.Padding)} INTEGER,
                    {nameof(Avatar.Text)} TEXT,
                    {nameof(Avatar.TextColor)} TEXT
                );";

            await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            throw;
        }

        _isInitialized = true;
    }
}