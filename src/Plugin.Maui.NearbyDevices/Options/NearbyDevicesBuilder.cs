namespace Plugin.Maui.NearbyDevices;

/// <summary>
/// Returned by <see cref="MauiAppBuilderExtensions.UseNearbyDevices"/> and
/// <see cref="ServiceCollectionExtensions.AddNearbyDevices"/> to allow opt-in
/// registration of Tier 2 services.
/// </summary>
public sealed class NearbyDevicesBuilder
{
    /// <summary>Gets the underlying service collection.</summary>
    public IServiceCollection Services { get; }

    internal NearbyDevicesBuilder(IServiceCollection services)
        => Services = services;
}
