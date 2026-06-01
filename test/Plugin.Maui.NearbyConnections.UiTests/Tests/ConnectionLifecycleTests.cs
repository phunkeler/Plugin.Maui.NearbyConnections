using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin.Maui.NearbyConnections.UiTests.Appium;

namespace Plugin.Maui.NearbyConnections.UiTests.Tests;

[TestClass]
public class ConnectionLifecycleTests
{
    public TestContext TestContext { get; set; } = null!;

    private static NearbyTestFixture? Fixture => TestAssemblySetup.Fixture;

    [TestInitialize]
    public void ResetState()
    {
        if (Fixture is null)
        {
            Assert.Inconclusive("DEVICE2_SERIAL not set — two-device tests skipped.");
        }

        Fixture.ResetBothToMainPage();
    }

    [TestMethod]
    public void FullLifecycle_BothDevicesShowConnected()
    {
        // Arrange
        var advertiser = Fixture!.Advertiser;
        var discoverer = Fixture!.Discoverer;

        // Act
        TestHelpers.EstablishConnection(advertiser, discoverer);
        TestHelpers.NavigateBothToConnectionsPage(advertiser, discoverer);

        Capture(advertiser, "05-connected");
        Capture(discoverer, "05-connected");

        // Assert
        var advertiserConnections = advertiser.WaitForElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));
        var discovererConnections = discoverer.WaitForElementsByPrefix("Disconnect_", TimeSpan.FromSeconds(10));

        Assert.IsTrue(advertiserConnections.Count > 0, "Advertiser shows no connected device on ConnectionsPage.");
        Assert.IsTrue(discovererConnections.Count > 0, "Discoverer shows no connected device on ConnectionsPage.");
    }

    private void Capture(AppiumAgent agent, string tag)
    {
        var path = TestHelpers.EvidencePath(tag, agent.Label);
        agent.Screenshot(path);
        TestContext.AddResultFile(path);
    }
}
