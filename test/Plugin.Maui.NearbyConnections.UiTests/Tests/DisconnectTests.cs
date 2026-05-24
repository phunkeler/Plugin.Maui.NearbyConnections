using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin.Maui.NearbyConnections.UiTests.Appium;

namespace Plugin.Maui.NearbyConnections.UiTests.Tests;

[TestClass]
public class DisconnectTests
{
    public TestContext TestContext { get; set; } = null!;

    private static NearbyTestFixture Fixture => TestAssemblySetup.Fixture;

    [TestInitialize]
    public void ResetState() => Fixture.ResetBothToMainPage();

    [TestMethod]
    public void Disconnect_BothDevicesObserveConnectionDrop()
    {
        // Arrange — connect and navigate both to ConnectionsPage
        var advertiser = Fixture.Advertiser;
        var discoverer = Fixture.Discoverer;

        TestHelpers.EstablishConnection(advertiser, discoverer);
        TestHelpers.NavigateBothToConnectionsPage(advertiser, discoverer);

        var disconnectIds = discoverer.WaitForElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(5));
        Assert.IsTrue(disconnectIds.Count > 0, "No Disconnect button on discoverer ConnectionsPage.");

        // Act
        discoverer.Tap(disconnectIds[0]);

        Capture(advertiser, "06-after-disconnect");
        Capture(discoverer, "06-after-disconnect");

        // Assert — Disconnect_ buttons disappear on both sides
        advertiser.WaitForNoElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));
        discoverer.WaitForNoElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));
    }

    private void Capture(AppiumAgent agent, string tag)
    {
        var path = TestHelpers.EvidencePath(tag, agent.Label);
        agent.Screenshot(path);
        TestContext.AddResultFile(path);
    }
}
