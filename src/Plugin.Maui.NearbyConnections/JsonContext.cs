using System.Text.Json.Serialization;
using Plugin.Maui.NearbyConnections.Events;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// JSON serialization context for Nearby Connections events.
/// </summary>
[JsonSerializable(typeof(INearbyConnectionsEvent))]
public partial class JsonContext : JsonSerializerContext
{

}