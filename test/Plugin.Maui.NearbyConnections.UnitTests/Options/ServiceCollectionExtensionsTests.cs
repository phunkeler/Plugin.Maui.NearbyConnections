using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    }
}
