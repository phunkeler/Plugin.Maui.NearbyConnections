using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Options")]
public class ServiceCollectionExtensionsTests
{
    [TestClass]
    public sealed class AddNearby : ServiceCollectionExtensionsTests
    {
        [TestMethod]
        public async Task NoLoggingRegistered_ResolvesWithoutThrowing()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddNearby(options => options.ServiceId = "test-service");

            // Act
            await using var provider = services.BuildServiceProvider();
            var session = provider.GetRequiredService<INearby>();

            // Assert
            Assert.IsNotNull(session);
        }

        [TestMethod]
        public async Task ResolvedTwice_ReturnsTheSameInstance()
        {
            // One radio, one native session — the singleton lifetime is platform-forced, not a
            // preference. Two instances would mean two MCSession/Nearby clients fighting over it.

            // Arrange
            var services = new ServiceCollection();
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
            services.AddNearby();

            // Assert
            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(ILoggerFactory)));
        }

        [TestMethod]
        public async Task Initializer_ConstructsTheSession_BeforeAnyoneResolvesIt()
        {
            // Devices.Changes does not replay, and the container builds singletons lazily. If the
            // initializer stops forcing construction, a connection established before the first
            // resolution is a transition nobody observed, and its payloads are silently lost.
            //
            // Asserting ordering, not identity: a test that only compares two resolutions passes
            // whether or not the initializer ran at all.
            var services = new ServiceCollection();
            services.AddNearby(options => options.ServiceId = "test-service");

            // A TimeProvider the session resolves during construction, so observing it tells us
            // exactly when construction happened.
            var clock = new ConstructionWitness();
            services.AddSingleton<TimeProvider>(clock.Resolve);

            // Arrange
            await using var provider = services.BuildServiceProvider();
            var initializer = provider.GetServices<IMauiInitializeService>()
                .Single(s => s.GetType().Name == "NearbySessionInitializer");

            Assert.IsFalse(clock.WasResolved, "Nothing may construct the session before startup runs.");

            // Act — this is what MauiAppBuilder.Build() does
            initializer.Initialize(provider);

            // Assert
            Assert.IsTrue(
                clock.WasResolved,
                "The initializer must construct the session at startup, not leave it to the first consumer.");
        }

        [TestMethod]
        public async Task EmptyServiceId_FailsValidationThroughTheContainer()
        {
            // NearbyOptionsValidator is covered in isolation elsewhere; this covers that AddNearby
            // actually registers it. Without the registration an unusable ServiceId would surface as
            // a confusing failure on the first advertise instead of at startup.
            //
            // Empty rather than malformed: the character rules live in ServiceIdRules and are
            // applied by NearbyOptionsValidator.ios.cs, so on net10.0 the null/empty check is the
            // whole of validation.
            var services = new ServiceCollection();
            services.AddNearby(options => options.ServiceId = "");

            // Arrange
            await using var provider = services.BuildServiceProvider();

            // Act + Assert
            var failure = Assert.ThrowsExactly<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<NearbyOptions>>().Value);

            Assert.IsNotEmpty(failure.Failures);
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
            // Act
            services.AddNearby(options => options.ServiceId = "test-service");

            // Assert
            // Assert
            await using var provider = services.BuildServiceProvider();
            Assert.AreSame(stub, provider.GetRequiredService<INearby>());
        }

        [TestMethod]
        public async Task TimeProviderRegistered_TheSessionUsesItRatherThanTheSystemClock()
        {
            // The session falls back to TimeProvider.System, so a registered provider being ignored
            // would leave every timeout un-fakeable and silently wall-clock bound.
            var services = new ServiceCollection();
            services.AddNearby(options => options.ServiceId = "test-service");

            var clock = new ConstructionWitness();
            services.AddSingleton<TimeProvider>(clock.Resolve);

            // Arrange
            await using var provider = services.BuildServiceProvider();

            // Act
            _ = provider.GetRequiredService<INearby>();

            // Assert
            Assert.IsTrue(clock.WasResolved, "A registered TimeProvider must win over TimeProvider.System.");
        }

        [TestMethod]
        public async Task NoConfigureDelegate_StillValidates_RatherThanShippingAnUnusableServiceId()
        {
            // AddNearby() with no delegate skips services.Configure entirely, so nothing supplies a
            // ServiceId. That must fail at startup rather than reach the radio: an app cannot
            // advertise under a default identifier, and silently doing so would make every install
            // discoverable to every other app built on this plugin.

            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNearby();

            // Assert
            // Act
            await using var provider = services.BuildServiceProvider();

            // Assert
            Assert.ThrowsExactly<OptionsValidationException>(
                () => provider.GetRequiredService<IOptions<NearbyOptions>>().Value,
                "ServiceId has no usable default; omitting it must be caught, not tolerated.");
        }

        [TestMethod]
        public async Task ConfigureDelegate_LeavesUnsetOptionsAtTheirDefaults()
        {
            // Configuring one option must not zero the rest: the delegate mutates the options
            // instance the pipeline built, it does not replace it.

            // Arrange
            var services = new ServiceCollection();
            var expectedTimeout = new NearbyOptions().InvitationTimeout;

            // Act
            services.AddNearby(options => options.ServiceId = "test-service");

            // Assert
            // Act
            await using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<NearbyOptions>>().Value;

            // Assert
            Assert.AreEqual("test-service", options.ServiceId);
            Assert.AreEqual(expectedTimeout, options.InvitationTimeout);
            Assert.AreEqual(NearbyTopology.Cluster, options.Android.Topology);
        }

        [TestMethod]
        public void CalledTwice_RegistersOnlyOneInitializer()
        {
            // MAUI runs these via GetServices<T>(), so a duplicate would construct the session twice
            // and — in a consuming app — double every startup subscription hung off it.

            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNearby();
            services.AddNearby();

            // Assert
            Assert.HasCount(
                1,
                services.Where(d => d.ServiceType == typeof(IMauiInitializeService)).ToList());
        }
    }
}
