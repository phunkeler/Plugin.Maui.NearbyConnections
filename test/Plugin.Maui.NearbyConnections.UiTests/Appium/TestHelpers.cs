namespace Plugin.Maui.NearbyConnections.UiTests.Appium;

internal static class TestHelpers
{
    /// <summary>
    /// Drives the full advertise → discover → connect handshake.
    /// Both agents must be on MainPage when this is called.
    /// On return both agents are on their respective feature pages
    /// (advertiser on AdvertisingPage, discoverer on DiscoveryPage).
    /// </summary>
    internal static void EstablishConnection(AppiumAgent advertiser, AppiumAgent discoverer)
    {
        advertiser.Tap("Advertise");
        advertiser.Tap("ToggleAdvertising");
        advertiser.WaitForText("AdvertisingStatus", "True", TimeSpan.FromSeconds(15));

        discoverer.Tap("Discover");
        discoverer.Tap("ToggleDiscovery");
        discoverer.WaitForText("DiscoveryStatus", "True", TimeSpan.FromSeconds(15));

        var connectIds = discoverer.WaitForElementsByPrefix("Connect_", TimeSpan.FromSeconds(30));
        discoverer.Tap(connectIds[0]);

        var acceptIds = advertiser.WaitForElementsByPrefix("Accept_", TimeSpan.FromSeconds(15));
        advertiser.Tap(acceptIds[0]);
    }

    /// <summary>
    /// Navigates both agents from their current feature page to ConnectionsPage
    /// by tapping BackButton then the Connections card.
    /// </summary>
    internal static void NavigateBothToConnectionsPage(AppiumAgent advertiser, AppiumAgent discoverer)
    {
        Parallel.Invoke(
            () => { advertiser.Tap("BackButton"); advertiser.Tap("Connections"); },
            () => { discoverer.Tap("BackButton"); discoverer.Tap("Connections"); });
    }

    internal static string EvidencePath(string tag, string label) =>
        Path.Combine("evidence", $"{tag}-{label}.png");
}
