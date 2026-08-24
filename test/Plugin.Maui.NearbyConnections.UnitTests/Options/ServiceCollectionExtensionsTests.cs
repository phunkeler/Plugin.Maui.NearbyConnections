using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Options")]
public class ServiceCollectionExtensionsTests
{
    [TestClass]
    public sealed class AddNearby : ServiceCollectionExtensionsTests
    {
        [TestMethod]
        public async Task NoLoggingRegistered_ThrowsOnResolution()
        {
            // The plugin resolves its loggers as required services rather than falling back to
            // NullLogger. A host that never registered logging gets an error naming the missing
            // service, instead of a session that silently never logs.

            // Arrange
            var services = new ServiceCollection();
            services.AddNearby(options => options.ServiceId = "test-service");
            await using var provider = services.BuildServiceProvider();

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(
                () => provider.GetRequiredService<INearby>());
        }

        [TestMethod]
        public async Task ResolvedTwice_ReturnsTheSameInstance()
        {
            // One radio, one native session — the singleton lifetime is platform-forced, not a
            // preference. Two instances would mean two MCSession/Nearby clients fighting over it.

            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddNearby(options => options.ServiceId = "test-service");

            // Act
            await using var provider = services.BuildServiceProvider();

            // Assert
            Assert.AreSame(
                provider.GetRequiredService<INearby>(),
                provider.GetRequiredService<INearby>());
        }

        [TestMethod]
        public void Always_DoesNotRegisterLoggingInfrastructure()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNearby(options => options.ServiceId = "test-service");

            // Assert
            Assert.DoesNotContain(d => d.ServiceType == typeof(ILoggerFactory), services);
        }

        [TestMethod]
        public void EmptyServiceId_FailsValidationImmediately()
        {
            // NearbyOptionsValidator is covered in isolation elsewhere; this covers that AddNearby
            // actually calls it. Validation runs synchronously inside AddNearby itself — not through
            // the Microsoft.Extensions.Options pipeline, which MAUI apps never trigger (there is no
            // IHost.StartAsync) — so the failure must surface from this call, not from a later
            // resolution.
            //
            // Empty rather than malformed: the character rules live in ServiceIdRules and are
            // applied by NearbyOptionsValidator.ios.cs, so on net10.0 the null/empty check is the
            // whole of validation.

            // Arrange
            var services = new ServiceCollection();

            // Act
            var failure = Assert.ThrowsExactly<ArgumentException>(
                () => services.AddNearby(options => options.ServiceId = ""));

            // Assert
            Assert.Contains("ServiceId", failure.Message);
        }

        [TestMethod]
        public void NoConfigureDelegate_StillValidates_RatherThanShippingAnUnusableServiceId()
        {
            // AddNearby() with no delegate never sets a ServiceId. That must fail immediately rather
            // than reach the radio: an app cannot advertise under a default identifier, and silently
            // doing so would make every install discoverable to every other app built on this plugin.

            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(
                () => services.AddNearby(),
                "ServiceId has no usable default; omitting it must be caught, not tolerated.");
        }

        [TestMethod]
        public async Task ConsumerRegisteredItsOwn_TryAddSingletonLeavesItAlone()
        {
            // TryAddSingleton, not AddSingleton: an app that supplies its own INearby — a fake in a
            // UI test, say — must keep it.

            // Arrange
            var services = new ServiceCollection();
            var stub = new StubNearby();
            services.AddSingleton<INearby>(stub);

            // Act
            services.AddNearby(options => options.ServiceId = "test-service");

            // Assert
            await using var provider = services.BuildServiceProvider();
            Assert.AreSame(stub, provider.GetRequiredService<INearby>());
        }

        [TestMethod]
        public async Task TimeProviderRegistered_TheSessionUsesItRatherThanTheSystemClock()
        {
            // The session falls back to TimeProvider.System, so a registered provider being ignored
            // would leave every timeout un-fakeable and silently wall-clock bound.

            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            // Registered before AddNearby: AddNearby supplies TimeProvider.System via
            // TryAddSingleton, so a consumer's own provider must already be in the collection to
            // win. Registering one afterwards is too late.
            var clock = new ConstructionWitness();
            services.AddSingleton<TimeProvider>(clock.Resolve);

            services.AddNearby(options => options.ServiceId = "test-service");

            await using var provider = services.BuildServiceProvider();

            // Act
            _ = provider.GetRequiredService<INearby>();

            // Assert
            Assert.IsTrue(clock.WasResolved, "A registered TimeProvider must win over TimeProvider.System.");
        }

        [TestMethod]
        public void CalledTwice_DoesNotThrow()
        {
            // AddNearby has no MAUI-lifecycle registration left to duplicate: it registers only
            // INearby itself, via TryAddSingleton, so calling it twice is a harmless no-op on the
            // second call rather than a duplicate-registration error.

            // Arrange
            var services = new ServiceCollection();

            // Act & Assert
            services.AddNearby(options => options.ServiceId = "test-service");
            services.AddNearby(options => options.ServiceId = "test-service");
        }
    }
}
