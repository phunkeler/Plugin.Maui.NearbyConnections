namespace NearbyChat.UiTests.Tests;

public class BytesTransferTests
{
    const string TestMessage = "e2e-ping";

    [Fact]
    public void SendTextMessage_AppearsOnReceiverSide()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var advertiser = fixture.Advertiser;
        var discoverer = fixture.Discoverers[0];
        fixture.ResetAllToMainPage();

        TestHelpers.EstablishConnection(advertiser, [discoverer]);
        TestHelpers.NavigateAllToConnectionsPage(advertiser, [discoverer]);

        var chatIds = advertiser.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        Assert.True(chatIds.Count > 0, "No Chat button on advertiser ConnectionsPage.");
        advertiser.Tap(chatIds[0]);
        advertiser.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));
        advertiser.Fill("ChatMessageEntry", TestMessage);

        advertiser.Screenshot(TestHelpers.EvidencePath("03-before-send", advertiser.Label));
        advertiser.Tap("ChatSendButton");

        var discovererChatIds = discoverer.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        Assert.True(discovererChatIds.Count > 0, "No Chat button on discoverer ConnectionsPage.");
        discoverer.Tap(discovererChatIds[0]);
        discoverer.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));

        discoverer.WaitForElementByText(TestMessage, TimeSpan.FromSeconds(20));
        discoverer.Screenshot(TestHelpers.EvidencePath("04-message-received", discoverer.Label));
    }
}
