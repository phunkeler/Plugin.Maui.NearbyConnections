namespace NearbyChat.UiTests.Tests;

public class SmokeTests
{
    [Fact]
    public void AppLaunches_MainPageVisible()
    {
        Assert.SkipWhen(AssemblySetup.Fixture is null, "DEVICE1_SERIAL not set.");
        var fixture = AssemblySetup.Fixture
            ?? throw new InvalidOperationException("Fixture is null after skip guard.");

        var agent = fixture.Advertiser;
        try
        {
            agent.WaitForElement("Advertise", TimeSpan.FromSeconds(15));
            agent.WaitForElement("Discover", TimeSpan.FromSeconds(5));
            agent.Screenshot(TestHelpers.EvidencePath("01-smoke-main", agent.Label));
        }
        catch
        {
            agent.Screenshot(TestHelpers.EvidencePath("01-smoke-main-FAILURE", agent.Label));
            agent.DumpPageSource(TestHelpers.EvidencePath("01-smoke-main-FAILURE", agent.Label) + ".xml");
            throw;
        }
    }
}
