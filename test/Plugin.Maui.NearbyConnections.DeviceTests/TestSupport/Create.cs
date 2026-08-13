namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>
/// Constructs the real <see cref="PlatformNearby"/> under test, wired with real SDK-backed
/// dependencies (no fakes — see <c>Plugin.Maui.NearbyConnections.UnitTests.TestSupport.Create</c>
/// for why the unit suite uses <c>net10.0</c>'s stub instead).
/// </summary>
/// <remarks>
/// The platform halves live in <c>Create.android.cs</c> and <c>Create.ios.cs</c>. Both declare the
/// same member names with platform-specific parameters, so a test body reads identically on either
/// target and this file stays free of conditional compilation.
/// </remarks>
static partial class Create
{
    /// <summary>The service id every device test advertises under.</summary>
    const string ServiceId = "devtest";

    /// <summary>The options the platform is wired with unless a test supplies its own.</summary>
    static NearbyOptions DefaultOptions() => new() { ServiceId = ServiceId };
}
