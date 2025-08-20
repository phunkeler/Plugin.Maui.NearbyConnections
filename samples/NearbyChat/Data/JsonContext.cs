using System.Text.Json.Serialization;
using NearbyChat.Models;

namespace NearbyChat.Data;

[JsonSerializable(typeof(User))]
public partial class JsonContext : JsonSerializerContext
{

}