using Microsoft.Extensions.Options;

namespace Plugin.Maui.NearbyDevices;

// Reserved for future mandatory internal configuration.
// Validation is handled separately by NearbyDevicesOptionsValidator.
sealed class NearbyDevicesOptionsSetup : IConfigureOptions<NearbyDevicesOptions>
{
    public void Configure(NearbyDevicesOptions options)
    {
    }
}
