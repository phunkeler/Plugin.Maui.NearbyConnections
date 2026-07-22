namespace NearbyChat.UiTests.Tests;

public class VideoAttachmentTests
{
    const string VideoFilename = "nearbychat_test.mp4";
    const string DeviceVideoPath = "/sdcard/Movies/" + VideoFilename;

    [Fact]
    public void VideoAttachment_ReceivedOnPeer()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var advertiser = fixture.Advertiser;
        var discoverer = fixture.Discoverers[0];
        fixture.ResetAllToMainPage();

        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "test_video.mp4");
        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException("Test video asset missing.", assetPath);
        }

        var localTemp = Path.Combine(Path.GetTempPath(), VideoFilename);
        File.Copy(assetPath, localTemp, overwrite: true);
        advertiser.PushFile(localTemp, DeviceVideoPath);

        TestHelpers.EstablishConnection(advertiser, [discoverer]);
        TestHelpers.NavigateAllToConnectionsPage(advertiser, [discoverer]);

        var discovererChatIds = discoverer.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        discoverer.Tap(discovererChatIds[0]);
        discoverer.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));

        var chatIds = advertiser.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        advertiser.Tap(chatIds[0]);
        advertiser.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));

        advertiser.Tap("ChatAttachButton");
        advertiser.WaitForElementByText("Video", TimeSpan.FromSeconds(5));
        advertiser.TapByText("Video");

        // Primary: file visible in the picker by text.
        // Fallback: UiScrollable.scrollIntoView if it's below the fold.
        try
        {
            advertiser.WaitForElementByText(VideoFilename, TimeSpan.FromSeconds(8));
            advertiser.TapByText(VideoFilename);
        }
        catch (WebDriverTimeoutException)
        {
            advertiser.ScrollIntoViewByText(VideoFilename);
        }

        advertiser.WaitForElement("ChatSendButton", TimeSpan.FromSeconds(5));
        advertiser.Screenshot(TestHelpers.EvidencePath("08-video-selected", advertiser.Label));
        advertiser.Tap("ChatSendButton");
        advertiser.Screenshot(TestHelpers.EvidencePath("08-video-sent", advertiser.Label));

        discoverer.WaitForElementContainingText(VideoFilename, TimeSpan.FromSeconds(30));
        discoverer.Screenshot(TestHelpers.EvidencePath("09-video-received", discoverer.Label));
    }
}
