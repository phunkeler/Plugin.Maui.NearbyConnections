// The whole suite runs serially. `PlatformBridge.StagingDirectory` is static and process-wide, and
// every `DisposeAsync` sweeps it (`PlatformSweepStaging`) — so any test that disposes a platform can
// delete a file another test staged. Nearly every test disposes one, via `await using var platform`,
// which makes per-class opt-in the wrong shape: it is an allowlist that has already been missed
// twice. Serialising the assembly enforces the invariant by construction. The suite runs on one
// device in a couple of seconds, so the parallelism was buying nothing.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Plugin.Maui.NearbyConnections.DeviceTests;

/// <summary>Anchor type for locating this assembly from the runner host via <c>typeof(...).Assembly</c>.</summary>
public sealed class AssemblyMarker;

/// <summary>
/// Marks the test classes that read or write the staging directory.
/// </summary>
/// <remarks>
/// <c>PlatformBridge.StagingDirectory</c> is static and process-wide, and every disposal sweeps it
/// (<c>PlatformSweepStaging</c>), so a disposal in one class can delete another class's staged file
/// mid-test. What prevents that is the assembly-level
/// <see cref="CollectionBehaviorAttribute.DisableTestParallelization"/> at the top of this file, not
/// this collection — membership here does not confer isolation on its own. The collection stays
/// because it names the classes that depend on staging isolation, which is the set to check first
/// when a staged file goes missing.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class StagingTests
{
    /// <summary>The collection name test classes reference.</summary>
    public const string Name = "Staging";
}
