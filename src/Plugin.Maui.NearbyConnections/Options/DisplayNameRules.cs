using System.Text;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The rules Apple applies to a Multipeer Connectivity <c>displayName</c>, expressed as pure string
/// validation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is a crash guard</strong>, for the same reason as <see cref="ServiceIdRules"/>. An
/// invalid <c>displayName</c> makes <c>MCPeerID</c>'s native initializer raise an
/// <c>NSInvalidArgumentException</c>, which crosses the native/managed boundary as a fatal native
/// crash — <em>not</em> a catchable .NET exception. A consumer cannot defend against it with
/// <c>try</c>/<c>catch</c>, so the only effective defence is to reject the value before it ever
/// reaches the platform.
/// </para>
/// <para>
/// <strong>This is reachable without any consumer mistake.</strong>
/// <c>NearbyOptions.DisplayName</c> defaults to <c>DeviceInfo.Name</c>, the name the user gave their
/// device. The limit is counted in UTF-8 bytes, so a name in a script that encodes to three bytes
/// per character exceeds it at about 21 characters — an ordinary Japanese or Arabic device name
/// crashes iOS advertising while the equivalent English name is nowhere near the bound.
/// </para>
/// <para>
/// Deliberately free of any iOS type reference, and compiled on every target framework, so the rules
/// can be unit tested on the plain <c>net10.0</c> target. Placing them in
/// <c>NearbyOptionsValidator.ios.cs</c> would have shipped the guard untested, because the unit test
/// project targets <c>net10.0</c> and never compiles the iOS partial.
/// </para>
/// <para>
/// Rules quoted from Apple's <c>MCPeerID</c> initializer documentation: <em>"The maximum allowable
/// length is 63 bytes in UTF-8 encoding"</em>, and <em>"The displayName parameter may not be nil or
/// an empty string"</em> — <em>"This method throws an exception if the displayName value is too
/// long, empty, or nil."</em>
/// </para>
/// </remarks>
static class DisplayNameRules
{
    /// <summary>
    /// The longest local display name Multipeer Connectivity accepts, in UTF-8 bytes.
    /// </summary>
    /// <remarks>
    /// Bytes, not characters, because that is the unit Apple specifies. Distinct from
    /// <see cref="PeerLookup.MaxDisplayNameBytes"/>, which bounds an inbound <em>remote</em> name and
    /// is this library's own choice — this one is the platform's hard limit on the name this device
    /// advertises.
    /// </remarks>
    internal const int MaxBytes = 63;

    const string Reference =
        "See https://developer.apple.com/documentation/multipeerconnectivity/mcpeerid/init(displayname:).";

    /// <summary>
    /// Adds a failure message for every rule <paramref name="displayName"/> violates.
    /// </summary>
    /// <param name="displayName">The configured value.</param>
    /// <param name="failures">The list each violation is appended to.</param>
    /// <remarks>
    /// Every rule is evaluated rather than returning at the first failure, so a developer fixing a
    /// value learns about all of its problems at once instead of one per rebuild.
    /// </remarks>
    internal static void Validate(string? displayName, List<string> failures)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            failures.Add(
                "DisplayName must not be null or empty. On iOS it is passed directly as MCPeerID's " +
                "displayName, which rejects both and raises a native exception that cannot be " +
                "caught. " + Reference);
            return;
        }

        var bytes = Encoding.UTF8.GetByteCount(displayName);

        if (bytes > MaxBytes)
        {
            failures.Add(
                $"DisplayName '{displayName}' is {bytes} UTF-8 bytes. On iOS it must be at most " +
                $"{MaxBytes}. Note the limit counts bytes rather than characters, so a name outside " +
                "the ASCII range reaches it sooner than its length suggests. " + Reference);
        }
    }
}
