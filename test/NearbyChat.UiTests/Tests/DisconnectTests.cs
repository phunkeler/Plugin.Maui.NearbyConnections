namespace NearbyChat.UiTests.Tests;

public class DisconnectTests
{
    [Fact]
    public void Disconnect_BothDevicesObserveConnectionDrop()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var advertiser = fixture.Advertiser;
        var discoverer = fixture.Discoverers[0];
        fixture.ResetAllToMainPage();

        TestHelpers.EstablishConnection(advertiser, [discoverer]);
        TestHelpers.NavigateAllToConnectionsPage(advertiser, [discoverer]);

        var disconnectIds = discoverer.WaitForElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(5));
        Assert.True(disconnectIds.Count > 0, "No Disconnect button on discoverer.");
        discoverer.Tap(disconnectIds[0]);

        advertiser.Screenshot(TestHelpers.EvidencePath("05-after-disconnect", advertiser.Label));
        discoverer.Screenshot(TestHelpers.EvidencePath("05-after-disconnect", discoverer.Label));

        advertiser.WaitForNoElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));
        discoverer.WaitForNoElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));
    }
}
