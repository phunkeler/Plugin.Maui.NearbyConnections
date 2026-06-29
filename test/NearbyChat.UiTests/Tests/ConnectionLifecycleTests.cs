namespace NearbyChat.UiTests.Tests;

public class ConnectionLifecycleTests
{
    [Fact]
    public void FullLifecycle_BothDevicesShowConnected()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var advertiser = fixture.Advertiser;
        var discoverer = fixture.Discoverers[0];
        fixture.ResetAllToMainPage();

        TestHelpers.EstablishConnection(advertiser, [discoverer]);
        TestHelpers.NavigateAllToConnectionsPage(advertiser, [discoverer]);

        advertiser.Screenshot(TestHelpers.EvidencePath("02-connected", advertiser.Label));
        discoverer.Screenshot(TestHelpers.EvidencePath("02-connected", discoverer.Label));

        var advConns = advertiser.WaitForElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));
        var discoConns = discoverer.WaitForElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));

        Assert.True(advConns.Count > 0, "Advertiser shows no connected device.");
        Assert.True(discoConns.Count > 0, "Discoverer shows no connected device.");
    }
}
