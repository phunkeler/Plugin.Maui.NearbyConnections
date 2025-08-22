using System.Text.Json.Serialization;
using NearbyChat.Models;

namespace NearbyChat.Data;

[JsonSerializable(typeof(AvatarsJson))]
public partial class JsonContext : JsonSerializerContext
{

}