using Microsoft.VisualStudio.TestTools.UnitTesting;
using Plugin.Maui.NearbyConnections.UiTests.Appium;

namespace Plugin.Maui.NearbyConnections.UiTests.Tests;

[TestClass]
public static class TestAssemblySetup
{
    internal static NearbyTestFixture Fixture { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task Initialize(TestContext _)
    {
        var device1 = NearbyTestFixture.RequiredEnv("DEVICE1_SERIAL");
        var device2 = NearbyTestFixture.RequiredEnv("DEVICE2_SERIAL");

        await DevicePrep.PrepareAsync(device1, device2);

        Fixture = NearbyTestFixture.FromEnvironment();
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        Fixture.Dispose();
    }
}
