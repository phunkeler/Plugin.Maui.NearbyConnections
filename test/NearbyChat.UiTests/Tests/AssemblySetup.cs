using Xunit.v3;

[assembly: AssemblyFixture(typeof(NearbyChat.UiTests.Tests.AssemblySetup))]

namespace NearbyChat.UiTests.Tests;

public sealed class AssemblySetup : IAsyncLifetime
{
    internal static NearbyTestFixture? Fixture { get; private set; }

    public ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("DEVICE1_SERIAL") is null)
        {
            return ValueTask.CompletedTask;
        }

        Fixture = NearbyTestFixture.FromEnvironment();

        var appPackage = NearbyTestFixture.GetEnv("APP_PACKAGE", "com.phunkeler.nearbychat");
        Parallel.ForEach(Fixture.All, agent => agent.GrantAllPermissions(appPackage));

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Fixture?.Dispose();
        return ValueTask.CompletedTask;
    }
}
