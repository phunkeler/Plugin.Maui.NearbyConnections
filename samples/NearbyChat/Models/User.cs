namespace NearbyChat.Models;

public class User
{
    string Id { get; set; } = string.Empty;
    string Name { get; set; } = string.Empty;
    byte[] Avatar { get; set; } = [];
}