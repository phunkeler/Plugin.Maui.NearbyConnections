using System.Security.Cryptography;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PeerLookup
{
    internal const int MaxDisplayNameBytes = 64;
    internal const int DeviceIdBytes = 8;

    readonly ConcurrentDictionary<string, NearbyDevice> _peers = new(StringComparer.Ordinal);

    internal static string MintDeviceId()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(DeviceIdBytes));

    internal NearbyDevice Record(string deviceId, string? displayName)
        => _peers.GetOrAdd(deviceId, static (id, name) => new NearbyDevice(id, name), Sanitize(displayName));

    internal static string? Sanitize(string? displayName, int maxBytes = MaxDisplayNameBytes)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return null;
        }

        var builder = new StringBuilder(Math.Min(displayName.Length, maxBytes));
        var bytes = 0;

        foreach (var rune in displayName.EnumerateRunes())
        {
            if (IsRejected(rune))
            {
                continue;
            }

            if (bytes + rune.Utf8SequenceLength > maxBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            builder.Append(rune);
        }

        return builder.Length == 0
            ? null
            : builder.ToString();
    }

    static bool IsRejected(Rune rune)
        => rune.Value is >= 0x202A and <= 0x202E   // bidi embeddings and overrides
            or >= 0x2066 and <= 0x2069             // bidi isolates
            or <= 0x1F                             // C0 controls
            or >= 0x7F and <= 0x9F;                // DEL and the C1 controls

    internal bool TryGetDevice(string deviceId, [NotNullWhen(true)] out NearbyDevice? device)
        => _peers.TryGetValue(deviceId, out device);

    internal bool IsEmpty => _peers.IsEmpty;

    internal NearbyDevice? Remove(string deviceId)
    {
        PlatformRemove(deviceId);

        return _peers.TryRemove(deviceId, out var device)
            ? device
            : null;
    }

    internal void Clear()
    {
        PlatformClear();
        _peers.Clear();
    }

    partial void PlatformRemove(string deviceId);

    partial void PlatformClear();
}