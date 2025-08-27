using System.Text.Json.Serialization;

namespace Plugin.Maui.NearbyConnections;

[JsonSerializable(typeof(NearbyConnectionEvent))]
public partial class JsonContext : JsonSerializerContext
{

}