using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin.Maui.NearbyConnections.UiTests.Appium;

namespace Plugin.Maui.NearbyConnections.UiTests.Tests;

[TestClass]
public class SmokeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void AppLaunches_MainPageVisible()
    {
        // Arrange
        var serverUrl = new Uri(Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://localhost:4723");
        var serial = NearbyTestFixture.RequiredEnv("DEVICE1_SERIAL");
        var appPackage = Environment.GetEnvironmentVariable("APP_PACKAGE") ?? "com.phunkeler.nearbychat";

        using var agent = new AppiumAgent(serverUrl, serial, appPackage, $"smoke:{serial}");

        // Act
        var screenshotPath = TestHelpers.EvidencePath("smoke-main", agent.Label);
        agent.Screenshot(screenshotPath);
        TestContext.AddResultFile(screenshotPath);

        // Assert
        agent.WaitForElement("Advertise", TimeSpan.FromSeconds(10));
        agent.WaitForElement("Discover", TimeSpan.FromSeconds(5));
    }
}
