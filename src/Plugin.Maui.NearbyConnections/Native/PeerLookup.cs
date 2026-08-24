using System.Text;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PeerLookup
{
    readonly ConcurrentDictionary<string, NearbyDevice> _peers = new(StringComparer.Ordinal);

    /// <summary>
    /// The longest remote-supplied display name kept intact. Longer names are truncated.
    /// </summary>
    /// <remarks>
    /// Both platforms let the remote side choose this string, and neither bounds it. iOS caps an
    /// <c>MCPeerID</c> display name at 63 bytes, but Android's <c>EndpointName</c> has no documented
    /// limit, so the bound is enforced here rather than assumed from either SDK.
    /// </remarks>
    internal const int MaxDisplayNameLength = 64;

    public NearbyDevice Record(string key, string? displayName)
        => _peers.GetOrAdd(key, static (k, name) => new NearbyDevice(k, name), Sanitize(displayName));

    /// <summary>
    /// Makes a remote-supplied display name safe to log and to render, by removing control
    /// characters and bounding its length.
    /// </summary>
    /// <param name="displayName">The name as the remote device supplied it.</param>
    /// <returns>
    /// The sanitized name, or <see langword="null"/> when the input was <see langword="null"/> or
    /// contained nothing but control characters.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a trust boundary: the name arrives from an unauthenticated peer on a proximity
    /// network. Control characters are stripped because the name reaches an <see cref="ILogger"/>
    /// sink — a name carrying newlines can forge log records around the real one.
    /// </para>
    /// <para>
    /// Sanitizing here rather than at each call site is deliberate. Every device this library
    /// publishes is created through <see cref="Record"/>, on both platforms, so one filter covers
    /// the whole surface. It does <b>not</b> make the name trustworthy: it remains attacker-chosen
    /// and unverified, and must never be treated as identity.
    /// </para>
    /// </remarks>
    internal static string? Sanitize(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }

        var builder = new StringBuilder(Math.Min(displayName.Length, MaxDisplayNameLength));

        foreach (var rune in displayName.EnumerateRunes())
        {
            if (Rune.IsControl(rune))
            {
                continue;
            }

            if (builder.Length + rune.Utf16SequenceLength > MaxDisplayNameLength)
            {
                break;
            }

            builder.Append(rune);
        }

        return builder.Length == 0
            ? null
            : builder.ToString();
    }

    public bool TryGetDevice(string key, [NotNullWhen(true)] out NearbyDevice? device)
        => _peers.TryGetValue(key, out device);

    public bool IsEmpty => _peers.IsEmpty;

    public NearbyDevice? Remove(string key)
    {
        PlatformRemove(key);

        return _peers.TryRemove(key, out var device)
            ? device
            : null;
    }

    public void Clear()
    {
        PlatformClear();
        _peers.Clear();
    }

    partial void PlatformRemove(string key);

    partial void PlatformClear();
}