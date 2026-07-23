using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyDevices.UnitTests;

[TestCategory("Options")]
public class ServiceCollectionExtensionsTests
{
    [TestClass]
    public sealed class AddNearbyDevices : ServiceCollectionExtensionsTests
    {
        [TestMethod]
        public async Task NoLoggingRegistered_ResolvesWithoutThrowing()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddNearbyDevices(options => options.ServiceId = "test-service");

            // Act
            await using var provider = services.BuildServiceProvider();
            var nearbyDevices = provider.GetRequiredService<INearbyDevices>();

            // Assert
            Assert.IsNotNull(nearbyDevices);
        }

        [TestMethod]
        public void Always_DoesNotRegisterLoggingInfrastructure()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddNearbyDevices();

            // Assert
            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(ILoggerFactory)));
        }
    }
}
