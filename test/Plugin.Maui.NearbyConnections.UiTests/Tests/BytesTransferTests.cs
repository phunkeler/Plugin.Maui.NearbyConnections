using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin.Maui.NearbyConnections.UiTests.Appium;

namespace Plugin.Maui.NearbyConnections.UiTests.Tests;

[TestClass]
public class BytesTransferTests
{
    private const string TestMessage = "e2e-ping";

    public TestContext TestContext { get; set; } = null!;

    private static NearbyTestFixture Fixture => TestAssemblySetup.Fixture;

    [TestInitialize]
    public void ResetState() => Fixture.ResetBothToMainPage();

    [TestMethod]
    public void SendMessage_MessageAppearsOnReceiverSide()
    {
        // Arrange — connect and navigate both to ConnectionsPage
        var advertiser = Fixture.Advertiser;
        var discoverer = Fixture.Discoverer;

        TestHelpers.EstablishConnection(advertiser, discoverer);
        TestHelpers.NavigateBothToConnectionsPage(advertiser, discoverer);

        // Act — advertiser opens chat to discoverer and sends message
        var chatIds = advertiser.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        Assert.IsTrue(chatIds.Count > 0, "No Chat button on advertiser ConnectionsPage.");
        advertiser.Tap(chatIds[0]);

        advertiser.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));
        advertiser.Fill("ChatMessageEntry", TestMessage);

        Capture(advertiser, "07-message-sent");
        advertiser.Tap("ChatSendButton");

        // Discoverer opens chat and waits for the message to arrive
        var discovererChatIds = discoverer.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        Assert.IsTrue(discovererChatIds.Count > 0, "No Chat button on discoverer ConnectionsPage.");
        discoverer.Tap(discovererChatIds[0]);
        discoverer.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));

        // Assert
        discoverer.WaitForElementByText(TestMessage, TimeSpan.FromSeconds(15));
        Capture(discoverer, "08-message-received");
    }

    private void Capture(AppiumAgent agent, string tag)
    {
        var path = TestHelpers.EvidencePath(tag, agent.Label);
        agent.Screenshot(path);
        TestContext.AddResultFile(path);
    }
}
