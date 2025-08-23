using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NearbyChat.Models;

/// <summary>
/// Represents a local user (the current device's user)
/// </summary>
public partial class User : ObservableObject
{
    // TEXT PRIMARY KEY
    string _id = string.Empty;
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    // TEXT NOT NULL
    string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    // INTEGER (foreign key to Avatar.Id)
    int _avatarId;
    public int AvatarId
    {
        get => _avatarId;
        set => SetProperty(ref _avatarId, value);
    }

    // TEXT (ISO 8601 date format)
    string _createdOn = string.Empty;
    public string CreatedOn
    {
        get => _createdOn;
        set => SetProperty(ref _createdOn, value);
    }

    [JsonIgnore]
    public Avatar? Avatar { get; set; }
}

public class UsersJson
{
    public List<User> Users { get; set; } = [];
}
