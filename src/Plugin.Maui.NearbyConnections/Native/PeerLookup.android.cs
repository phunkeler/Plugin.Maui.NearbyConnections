namespace Plugin.Maui.NearbyConnections;

sealed partial class PeerLookup
{
    /// <summary>
    /// The two directions of the map between a Nearby Connections endpoint id and the device id this
    /// library mints for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android's native handle is a <see cref="string"/>, so both directions are plain dictionaries.
    /// The iOS partial needs no equivalent: an <c>MCPeerID</c> is an object, and the platform hands
    /// back the same instance for a peer, so object identity is the map there.
    /// </para>
    /// <para>
    /// Endpoint ids never leave this type. Everything above <c>Native/</c> — the registry, the
    /// channels, the pending-handshake map, and the public surface — deals only in device ids.
    /// </para>
    /// </remarks>
    readonly ConcurrentDictionary<string, string> _deviceIdByEndpoint = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, string> _endpointByDeviceId = new(StringComparer.Ordinal);

    /// <summary>
    /// The device id for a Nearby Connections endpoint, minted on first sight.
    /// </summary>
    /// <remarks>
    /// Call this at the top of every callback that receives an endpoint id from the SDK. Google's
    /// endpoint id is a session-scoped token of the platform's own choosing; publishing it directly
    /// would put a vendor value on the public surface and give <see cref="NearbyDevice.Id"/> a
    /// different shape per platform.
    /// </remarks>
    public string DeviceIdFor(string endpointId)
        => _deviceIdByEndpoint.GetOrAdd(
            endpointId,
            static (endpoint, self) =>
            {
                var deviceId = MintDeviceId();

                self._endpointByDeviceId[deviceId] = endpoint;

                return deviceId;
            },
            this);

    /// <summary>
    /// Resolves a device id back to the endpoint id the SDK expects.
    /// </summary>
    /// <remarks>
    /// Every call into <c>ConnectionsClient</c> that names a peer goes through here. A device id is
    /// meaningless to Google's SDK, so passing one straight through would fail at run time rather
    /// than at compile time — this method is the seam that makes the translation explicit.
    /// </remarks>
    public bool TryGetEndpointId(string deviceId, [NotNullWhen(true)] out string? endpointId)
        => _endpointByDeviceId.TryGetValue(deviceId, out endpointId);

    partial void PlatformRemove(string key)
    {
        // Both directions, or the reverse map grows for the session and a peer rediscovered under a
        // fresh endpoint id resolves against a stale entry.
        if (_endpointByDeviceId.TryRemove(key, out var endpointId))
        {
            _deviceIdByEndpoint.TryRemove(endpointId, out _);
        }
    }

    partial void PlatformClear()
    {
        _deviceIdByEndpoint.Clear();
        _endpointByDeviceId.Clear();
    }
}
