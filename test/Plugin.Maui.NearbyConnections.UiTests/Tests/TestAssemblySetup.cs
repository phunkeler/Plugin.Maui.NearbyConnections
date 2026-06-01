using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin.Maui.NearbyConnections.UiTests.Appium;

namespace Plugin.Maui.NearbyConnections.UiTests.Tests;

[TestClass]
public static class TestAssemblySetup
{
    internal static NearbyTestFixture? Fixture { get; private set; }

    [AssemblyInitialize]
    public static Task Initialize(TestContext _)
    {
        var device2 = Environment.GetEnvironmentVariable("DEVICE2_SERIAL");
        if (device2 is not null)
        {
            Fixture = NearbyTestFixture.FromEnvironment();
        }

        return Task.CompletedTask;
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        Fixture?.Dispose();
    }
}
