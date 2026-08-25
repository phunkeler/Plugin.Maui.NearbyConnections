using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Plugin.Maui.NearbyConnections;

sealed partial class PeerLookup
{
    readonly ConcurrentDictionary<string, NearbyDevice> _peers = new(StringComparer.Ordinal);

    /// <summary>
    /// The longest remote-supplied display name kept intact, counted in UTF-8 bytes. Longer names
    /// are truncated on a rune boundary.
    /// </summary>
    /// <remarks>
    /// Bytes, not characters: it is the unit both platforms constrain, and the unit a log sink and
    /// a wire format actually cost. 64 is this library's own bound — Android's <c>EndpointName</c>
    /// has no documented limit. iOS caps a *local* <c>MCPeerID</c> display name at 63 bytes, which
    /// is a different value bounding a different string: that one is built from
    /// <c>NearbyOptions.DisplayName</c>, not from anything a remote peer sends, and is validated by
    /// <see cref="DisplayNameRules"/>.
    /// </remarks>
    internal const int MaxDisplayNameBytes = 64;

    /// <summary>
    /// The size of a device id in bytes, rendered as twice as many hex characters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Eight bytes, balancing collision resistance against cost. Nearby Connections supports a
    /// handful of simultaneous peers and Multipeer Connectivity caps a session at eight, so the
    /// realistic population is tens of ids per session. By the birthday bound, 64 bits gives a
    /// collision probability around 1e-16 at a hundred peers and 2.7e-08 at a million — orders of
    /// magnitude below the radio failures this library already has to tolerate.
    /// </para>
    /// <para>
    /// A wider id buys nothing here. A narrower one, or a counter, would be enumerable: an id that
    /// increments tells an observer how many peers this session has seen and lets two logs be lined
    /// up against each other. Randomness is what keeps the id inert.
    /// </para>
    /// <para>
    /// Cost is not a factor at this size. Minting is paid once per peer discovered — never per
    /// payload, per callback, or per log line — and measures a few hundred nanoseconds.
    /// </para>
    /// </remarks>
    internal const int DeviceIdBytes = 8;

    /// <summary>
    /// Mints a device id. The single definition of the identifier's shape, on every platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The id is generated, never derived.</b> It is not a hash, an encoding, or a transformation
    /// of anything either SDK supplied, which is what lets the public contract be identical on
    /// Android and iOS: opaque, session-scoped, uncorrelatable, and carrying no identity data.
    /// </para>
    /// <para>
    /// Deriving could not promise that on both platforms. Android exposes nothing device-specific to
    /// derive from except the display name, and an id derived from a display name both collides
    /// same-named devices and puts identity data back into the identifier. iOS could derive from the
    /// archived <c>MCPeerID</c>, and did — but that archive contains the display name, so the id was
    /// a reversible pseudonym of a low-entropy string and needed a salt to be safe. Generating makes
    /// the whole problem absent rather than mitigated.
    /// </para>
    /// <para>
    /// <see cref="RandomNumberGenerator"/> rather than <see cref="Random"/>: the id is a
    /// security-relevant token on a surface that faces unauthenticated peers, and a predictable
    /// sequence would let an observer correlate or anticipate ids. The cost difference is irrelevant
    /// at one call per discovered peer.
    /// </para>
    /// </remarks>
    internal static string MintDeviceId()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(DeviceIdBytes));

    /// <summary>
    /// Records what a platform callback saw for <paramref name="deviceId"/>, and returns the device the
    /// library publishes for it.
    /// </summary>
    /// <param name="deviceId">The device id this library minted for the peer.</param>
    /// <param name="displayName">
    /// The name as the remote device supplied it. Sanitized here — see <see cref="Sanitize"/>.
    /// </param>
    /// <returns>
    /// The device now stored for <paramref name="deviceId"/> — the existing instance if there was one.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The first name wins, for the life of the session.</b> A later callback reporting a
    /// different name for the same device id is discarded, and the original instance is returned
    /// unchanged. Both platforms re-report a peer routinely — Android on every
    /// <c>OnEndpointFound</c>, iOS on every <c>Track</c> — so a device that starts advertising a new
    /// name mid-session keeps displaying the name it was first seen under.
    /// </para>
    /// <para>
    /// That is deliberate, and <c>PeerLookupTests.Record.ExistingPeer_DoesNotAdoptNewDisplayName</c>
    /// pins it. The name is attacker-chosen and unverified — see <see cref="Sanitize"/> — so letting
    /// it change would let a peer relabel itself in a consumer's user interface after the user has
    /// already decided to trust it under one name. Pinning removes that moving target. It does not
    /// make the name trustworthy, and it is never identity: <paramref name="deviceId"/> is.
    /// </para>
    /// <para>
    /// <b>Do not change this to adopt the newer name</b> without changing that test deliberately.
    /// It reads as a defect on inspection, and has been reported as one.
    /// </para>
    /// </remarks>
    public NearbyDevice Record(string deviceId, string? displayName)
        => _peers.GetOrAdd(deviceId, static (id, name) => new NearbyDevice(id, name), Sanitize(displayName));

    /// <summary>
    /// Makes a remote-supplied display name safe to log and to render, by removing characters that
    /// can forge or disguise output and bounding its length.
    /// </summary>
    /// <param name="displayName">The name as the remote device supplied it.</param>
    /// <returns>
    /// The sanitized name, or <see langword="null"/> when the input was <see langword="null"/>,
    /// empty, or left nothing behind once the rejected categories were removed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a trust boundary: the name arrives from an unauthenticated peer on a proximity
    /// network, and it reaches both an <see cref="ILogger"/> sink and consumer user interface.
    /// </para>
    /// <para>
    /// Rejection is by Unicode category, not by <see cref="Rune.IsControl"/>. That method tests for
    /// <see cref="UnicodeCategory.Control"/> alone, which let two whole classes of attack through:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Log forging.</b> U+2028 and U+2029 are <see cref="UnicodeCategory.LineSeparator"/> and
    /// <see cref="UnicodeCategory.ParagraphSeparator"/>, not <c>Control</c>. Both break lines in
    /// common log formatters and viewers, so a name carrying one forges a whole record around the
    /// real one — the exact attack stripping <c>\n</c> was meant to stop.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Display spoofing.</b> <see cref="UnicodeCategory.Format"/> covers the bidirectional
    /// overrides (U+202E and friends) and the zero-width characters. An override reverses how the
    /// rest of the name renders; a zero-width character lets two distinct peers render identically.
    /// Both target the person deciding whether to trust the device, which is the decision this
    /// string most influences.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Private-use characters are rejected on the same principle: they carry no meaning a consumer
    /// can act on and render unpredictably per platform. So is U+FFFD, the replacement character —
    /// which is also how an unpaired surrogate reaches this method, because
    /// <see cref="string.EnumerateRunes"/> substitutes it for malformed input rather than yielding a
    /// surrogate rune.
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
            return null;
        }

        var builder = new StringBuilder(Math.Min(displayName.Length, MaxDisplayNameBytes));
        var bytes = 0;

        foreach (var rune in displayName.EnumerateRunes())
        {
            if (IsRejected(rune))
            {
                continue;
            }

            // Truncate on a rune boundary, so the cap can never split a surrogate pair or a
            // multi-byte sequence and leave an unrenderable fragment behind.
            if (bytes + rune.Utf8SequenceLength > MaxDisplayNameBytes)
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

    /// <summary>
    /// Whether a rune is removed by <see cref="Sanitize"/>. See the remarks there for why each
    /// category is rejected.
    /// </summary>
    static bool IsRejected(Rune rune)
        // U+FFFD is what string.EnumerateRunes substitutes for an unpaired surrogate, so this — not
        // UnicodeCategory.Surrogate — is how malformed input actually presents. Enumerating never
        // yields a Surrogate rune, which is why that category is not tested for here. Rejecting the
        // replacement character keeps a peer from planting a visible "unknown glyph" in its name,
        // and keeps a malformed name from rendering as one.
        => rune.Value == 0xFFFD
            || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.Format
                or UnicodeCategory.PrivateUse;

    public bool TryGetDevice(string deviceId, [NotNullWhen(true)] out NearbyDevice? device)
        => _peers.TryGetValue(deviceId, out device);

    public bool IsEmpty => _peers.IsEmpty;

    public NearbyDevice? Remove(string deviceId)
    {
        PlatformRemove(deviceId);

        return _peers.TryRemove(deviceId, out var device)
            ? device
            : null;
    }

    public void Clear()
    {
        PlatformClear();
        _peers.Clear();
    }

    partial void PlatformRemove(string deviceId);

    partial void PlatformClear();
}