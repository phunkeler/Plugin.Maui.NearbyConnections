namespace NearbyChat.UiTests.Helpers;

static class TestHelpers
{
    internal static void EstablishConnection(
        AppiumAgent advertiser,
        IReadOnlyList<AppiumAgent> discoverers)
    {
        try
        {
            // Tap("Advertise") starts a Shell page-transition animation to
            // AdvertisingPage. Without waiting for it to finish rendering,
            // the very next Tap("ToggleAdvertising") can fire mid-transition
            // and miss the button entirely (NoSuchElementException) —
            // confirmed via a failure screenshot showing MainPage and
            // AdvertisingPage both partially visible mid-slide. Wait for the
            // destination page's own button to actually appear before
            // acting on it.
            advertiser.Tap("Advertise");
            advertiser.WaitForElement("ToggleAdvertising", TimeSpan.FromSeconds(5));
            advertiser.Tap("ToggleAdvertising");
            advertiser.WaitForText("AdvertisingStatus", "True", TimeSpan.FromSeconds(15));

            Parallel.ForEach(discoverers, d =>
            {
                d.Tap("Discover");
                d.WaitForElement("ToggleDiscovery", TimeSpan.FromSeconds(5));
                d.Tap("ToggleDiscovery");
                d.WaitForText("DiscoveryStatus", "True", TimeSpan.FromSeconds(15));
            });

            // Connect in parallel; accept serially (one Accept dialog at a time).
            //
            // The underlying Nearby Connections handshake is a real
            // WiFi-LAN/BLE encrypted channel negotiation between physical
            // devices and can genuinely fail mid-handshake on real hardware —
            // confirmed via logcat showing a "safe-to-disconnect" protocol
            // EOFException tearing down the channel seconds after connect,
            // with no app-level exception on either side (not a code bug,
            // a real transient RF/channel failure). Retry the tap-connect/
            // wait-for-accept cycle a few times before giving up, since a
            // fresh attempt after a dropped handshake reliably succeeds.
            const int maxHandshakeAttempts = 3;
            for (var attempt = 1; attempt <= maxHandshakeAttempts; attempt++)
            {
                try
                {
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

                    break;
                }
                catch when (attempt < maxHandshakeAttempts)
                {
                    // Let the dropped handshake fully settle before retrying —
                    // the peer needs to re-advertise its Connect_ button.
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
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

    static void DumpFailureEvidence(string tag, AppiumAgent agent)
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
                agent.WaitForElement("Connections", TimeSpan.FromSeconds(5));
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
    static readonly char[] s_artifactReservedChars = ['"', ':', '<', '>', '|', '*', '?', '\r', '\n'];

    internal static string EvidencePath(string tag, string label) =>
        Path.Combine(AppContext.BaseDirectory, "evidence", $"{tag}-{SanitizeForFileName(label)}.png");

    static string SanitizeForFileName(string value) =>
        string.Concat(value.Select(c => s_artifactReservedChars.Contains(c) ? '-' : c));
}
