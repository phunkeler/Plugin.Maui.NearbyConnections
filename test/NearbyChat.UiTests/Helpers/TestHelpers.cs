namespace NearbyChat.UiTests.Helpers;

internal static class TestHelpers
{
    internal static void EstablishConnection(
        AppiumAgent advertiser,
        IReadOnlyList<AppiumAgent> discoverers)
    {
        try
        {
            advertiser.Tap("Advertise");
            advertiser.Tap("ToggleAdvertising");
            advertiser.WaitForText("AdvertisingStatus", "True", TimeSpan.FromSeconds(15));

            Parallel.ForEach(discoverers, d =>
            {
                d.Tap("Discover");
                d.Tap("ToggleDiscovery");
                d.WaitForText("DiscoveryStatus", "True", TimeSpan.FromSeconds(15));
            });

            // Connect in parallel; accept serially (one Accept dialog at a time).
            Parallel.ForEach(discoverers, discoverer =>
            {
                var connectIds = discoverer.WaitForElementsByPrefix("Connect_", TimeSpan.FromSeconds(30));
                discoverer.Tap(connectIds[0]);
            });

            foreach (var _ in discoverers)
            {
                var acceptIds = advertiser.WaitForElementsByPrefix("Accept_", TimeSpan.FromSeconds(15));
                advertiser.Tap(acceptIds[0]);
            }
        }
        catch
        {
            DumpFailureEvidence("establish-connection-FAILURE", advertiser);
            foreach (var discoverer in discoverers)
            {
                DumpFailureEvidence("establish-connection-FAILURE", discoverer);
            }
            throw;
        }
    }

    private static void DumpFailureEvidence(string tag, AppiumAgent agent)
    {
        try
        {
            agent.Screenshot(EvidencePath(tag, agent.Label));
            agent.DumpPageSource(EvidencePath(tag, agent.Label) + ".xml");
        }
        catch
        {
            // Best-effort diagnostics — a failure capturing evidence must not
            // mask or replace the original test failure.
        }
    }

    internal static void NavigateAllToConnectionsPage(
        AppiumAgent advertiser,
        IReadOnlyList<AppiumAgent> discoverers)
    {
        Parallel.ForEach(
            new[] { advertiser }.Concat(discoverers),
            agent =>
            {
                agent.Tap("BackButton");
                agent.Tap("Connections");
            });
    }

    // VSTest runs tests in a testhost process whose working directory is not
    // guaranteed to be the invocation directory (dotnet test's CWD) — anchor
    // to the test assembly's own runtime directory instead, which is stable
    // regardless of how/where `dotnet test` was invoked from.
    //
    // AppiumAgent.Label embeds the device serial as "role:serial" (e.g.
    // "advertiser:R5CX..."). ':' is a valid filename character on the Linux
    // CI runner's filesystem (Path.GetInvalidFileNameChars() only rejects
    // NUL and '/' on Unix), but actions/upload-artifact rejects it anyway —
    // it enforces its own reserved-character list for cross-filesystem
    // portability, so sanitize against that list rather than the OS's.
    private static readonly char[] s_artifactReservedChars = ['"', ':', '<', '>', '|', '*', '?', '\r', '\n'];

    internal static string EvidencePath(string tag, string label) =>
        Path.Combine(AppContext.BaseDirectory, "evidence", $"{tag}-{SanitizeForFileName(label)}.png");

    private static string SanitizeForFileName(string value) =>
        string.Concat(value.Select(c => s_artifactReservedChars.Contains(c) ? '-' : c));
}
