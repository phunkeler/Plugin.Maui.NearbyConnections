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
            var nearbyConnections = provider.GetRequiredService<INearbyConnections>();

            // Assert
            Assert.IsNotNull(nearbyConnections);
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
