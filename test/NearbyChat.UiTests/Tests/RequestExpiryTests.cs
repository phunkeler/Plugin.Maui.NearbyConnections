namespace NearbyChat.UiTests.Tests;

/// <summary>
/// The inbound request expiry, end to end on real devices: a prompt nobody answers must withdraw
/// itself, and the advertiser's row must disappear without any tap.
/// </summary>
/// <remarks>
/// The request row expires at the offer's one deadline: the discoverer's <c>ConnectTimeout</c>,
/// declared on the wire. The sample sets it to 10 seconds in <c>MauiProgram</c>, so the waits
/// below are sized against that value rather than the library default.
/// </remarks>
public class RequestExpiryTests
{
    [Fact]
    public void UnansweredRequest_WithdrawsItselfFromTheAdvertiser()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var advertiser = fixture.Advertiser;
        var discoverer = fixture.Discoverers[0];
        fixture.ResetAllToMainPage();

        advertiser.Tap("Advertise");
        advertiser.WaitForElement("ToggleAdvertising", TimeSpan.FromSeconds(5));
        advertiser.Tap("ToggleAdvertising");
        advertiser.WaitForText("AdvertisingStatus", "True", TimeSpan.FromSeconds(15));

        discoverer.Tap("Discover");
        discoverer.WaitForElement("ToggleDiscovery", TimeSpan.FromSeconds(5));
        discoverer.Tap("ToggleDiscovery");
        discoverer.WaitForText("DiscoveryStatus", "True", TimeSpan.FromSeconds(15));

        var connectIds = discoverer.WaitForElementsByPrefix("Connect_", TimeSpan.FromSeconds(30));
        discoverer.Tap(connectIds[0]);

        // The prompt must appear, and is then deliberately left unanswered.
        var acceptIds = advertiser.WaitForElementsByPrefix("Accept_", TimeSpan.FromSeconds(15));
        Assert.True(acceptIds.Count > 0, "Advertiser never showed an Accept prompt.");

        advertiser.Screenshot(TestHelpers.EvidencePath("06-request-pending", advertiser.Label));

        // The row must go away on the library's own timer, with no interaction. Allowed well beyond
        // the sample's 10s ConnectTimeout (the declared offer window), because the discoverer's
        // connect attempt has to fail first on real hardware.
        advertiser.WaitForNoElementsByPrefix("Accept_", TimeSpan.FromSeconds(75));

        advertiser.Screenshot(TestHelpers.EvidencePath("06-request-expired", advertiser.Label));
    }
}
