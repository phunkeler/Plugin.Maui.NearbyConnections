using System.Text.Json.Serialization;
using NearbyChat.Models;

namespace NearbyChat.Data;

[JsonSerializable(typeof(AvatarsJson))]
[JsonSerializable(typeof(UsersJson))]
public partial class JsonContext : JsonSerializerContext
{

}