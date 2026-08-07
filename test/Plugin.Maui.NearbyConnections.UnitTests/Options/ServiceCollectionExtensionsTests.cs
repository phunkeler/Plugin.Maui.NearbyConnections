using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Options")]
public class ServiceCollectionExtensionsTests
{
    [TestClass]
    public sealed class AddNearbyConnections : ServiceCollectionExtensionsTests
    {
        [TestMethod]
        public async Task NoLoggingRegistered_ResolvesWithoutThrowing()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddNearbyConnections(options => options.ServiceId = "test-service");

            // Act
            await using var provider = services.BuildServiceProvider();
            var session = provider.GetRequiredService<INearbySession>();

            // Assert
            Assert.IsNotNull(session);
        }

        [TestMethod]
        public async Task ResolvedTwice_ReturnsTheSameInstance()
        {
            // One radio, one native session — the singleton lifetime is platform-forced, not a
            // preference. Two instances would mean two MCSession/Nearby clients fighting over it.
            var services = new ServiceCollection();
            services.AddNearbyConnections(options => options.ServiceId = "test-service");

            await using var provider = services.BuildServiceProvider();

            Assert.AreSame(
                provider.GetRequiredService<INearbySession>(),
                provider.GetRequiredService<INearbySession>());
        }

        [TestMethod]
        public void Always_DoesNotRegisterLoggingInfrastructure()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNearbyConnections();

            // Assert
            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(ILoggerFactory)));
        }

        [TestMethod]
        public async Task Initializer_ConstructsTheSessionBeforeAnyoneResolvesIt()
        {
            // ConnectionEstablished does not replay, and the container builds singletons lazily. If
            // the initializer stops forcing construction, a connection established before the first
            // resolution raises an event with no subscriber and its payloads are silently lost.
            var services = new ServiceCollection();
            services.AddNearbyConnections(options => options.ServiceId = "test-service");

            await using var provider = services.BuildServiceProvider();

            var initializer = provider.GetServices<IMauiInitializeService>()
                .Single(s => s.GetType().Name == "NearbySessionInitializer");

            initializer.Initialize(provider);

            // Same instance the initializer already built, not a second one made on demand.
            Assert.AreSame(
                provider.GetRequiredService<INearbySession>(),
                provider.GetRequiredService<INearbySession>());
        }

        [TestMethod]
        public void CalledTwice_RegistersOnlyOneInitializer()
        {
            // MAUI runs these via GetServices<T>(), so a duplicate would construct the session twice
            // and — in a consuming app — double every startup subscription hung off it.
            var services = new ServiceCollection();

            services.AddNearbyConnections();
            services.AddNearbyConnections();

            Assert.HasCount(
                1,
                services.Where(d => d.ServiceType == typeof(IMauiInitializeService)).ToList());
        }
    }
}
