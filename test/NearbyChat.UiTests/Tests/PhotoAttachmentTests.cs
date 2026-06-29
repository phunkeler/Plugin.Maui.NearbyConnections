namespace NearbyChat.UiTests.Tests;

public class PhotoAttachmentTests
{
    // Minimal 1×1 white JPEG — generated inline, no committed binary needed.
    private static readonly byte[] MinimalJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8U" +
        "HRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgN" +
        "DRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy" +
        "MjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAA" +
        "AAAAAAAAAAAAAP/EABQBAQAAAAAAAAAAAAAAAAAAAAD/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oA" +
        "DAMBAAIRAxEAPwCwABmX/9k=");

    private const string PhotoFilename = "nearbychat_test.jpg";
    private const string DevicePhotoPath = "/sdcard/Pictures/" + PhotoFilename;

    [Fact]
    public void PhotoAttachment_ReceivedOnPeer()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var advertiser = fixture.Advertiser;
        var discoverer = fixture.Discoverers[0];
        fixture.ResetAllToMainPage();

        var localTemp = Path.Combine(Path.GetTempPath(), PhotoFilename);
        File.WriteAllBytes(localTemp, MinimalJpeg);
        advertiser.PushFile(localTemp, DevicePhotoPath);

        TestHelpers.EstablishConnection(advertiser, [discoverer]);
        TestHelpers.NavigateAllToConnectionsPage(advertiser, [discoverer]);

        // Open chat on discoverer first so it's listening.
        var discovererChatIds = discoverer.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        discoverer.Tap(discovererChatIds[0]);
        discoverer.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));

        var chatIds = advertiser.WaitForElementsByPrefix("Chat_", TimeSpan.FromSeconds(5));
        advertiser.Tap(chatIds[0]);
        advertiser.WaitForElement("ChatMessageEntry", TimeSpan.FromSeconds(10));

        advertiser.Tap("ChatAttachButton");
        advertiser.WaitForElementByText("Photo", TimeSpan.FromSeconds(5));
        advertiser.TapByText("Photo");

        // Primary: file visible in the picker by text.
        // Fallback: UiScrollable.scrollIntoView if it's below the fold.
        try
        {
            advertiser.WaitForElementByText(PhotoFilename, TimeSpan.FromSeconds(8));
            advertiser.TapByText(PhotoFilename);
        }
        catch (WebDriverTimeoutException)
        {
            advertiser.ScrollIntoViewByText(PhotoFilename);
        }

        advertiser.WaitForElement("ChatSendButton", TimeSpan.FromSeconds(5));
        advertiser.Screenshot(TestHelpers.EvidencePath("06-photo-selected", advertiser.Label));
        advertiser.Tap("ChatSendButton");
        advertiser.Screenshot(TestHelpers.EvidencePath("06-photo-sent", advertiser.Label));

        // ChatViewModel sets Message = fileResult.FileName; received message text = filename.
        discoverer.WaitForElementContainingText(PhotoFilename, TimeSpan.FromSeconds(20));
        discoverer.Screenshot(TestHelpers.EvidencePath("07-photo-received", discoverer.Label));
    }
}
