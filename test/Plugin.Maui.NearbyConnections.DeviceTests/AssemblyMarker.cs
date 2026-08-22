namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>Anchor type for locating this assembly from the runner host via <c>typeof(...).Assembly</c>.</summary>
public sealed class AssemblyMarker;

/// <summary>
/// Serialises the test classes that read or write the staging directory.
/// </summary>
/// <remarks>
/// <c>PlatformNearby.StagingDirectory</c> is static and process-wide, and every disposal sweeps it
/// (<c>PlatformSweepStaging</c>). xUnit runs each test class as its own collection in parallel, so
/// without this a disposal in one class deletes another class's staged file mid-test. Any test that
/// disposes a platform or asserts on a staged file belongs in this collection.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class StagingTests
{
    /// <summary>The collection name test classes reference.</summary>
    public const string Name = "Staging";
}
