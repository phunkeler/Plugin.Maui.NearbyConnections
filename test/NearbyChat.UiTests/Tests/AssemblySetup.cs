using Xunit.v3;

[assembly: AssemblyFixture(typeof(NearbyChat.UiTests.Tests.AssemblySetup))]
// All test classes share the same 3 physical Android devices via
// AssemblySetup.Fixture. xUnit v3's default parallelizes test classes
// against each other, which drives the same devices' UI concurrently from
// multiple tests and produces StaleElementReferenceException /
// androidx.test.uiautomator.StaleObjectException. These tests must run
// strictly sequentially since they contend for shared hardware state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

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

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Fixture?.Dispose();
        return ValueTask.CompletedTask;
    }
}
