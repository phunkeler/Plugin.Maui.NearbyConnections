namespace NearbyChat.UiTests.Helpers;

internal static class TestHelpers
{
    internal static void EstablishConnection(
        AppiumAgent advertiser,
        IReadOnlyList<AppiumAgent> discoverers)
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

    internal static string EvidencePath(string tag, string label) =>
        Path.Combine("evidence", $"{tag}-{label}.png");
}
