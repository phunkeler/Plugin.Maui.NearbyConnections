namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Returned by <see cref="MauiAppBuilderExtensions.UseNearbyConnections"/> and
/// <see cref="ServiceCollectionExtensions.AddNearbyConnections"/> to allow opt-in
/// registration of Tier 2 services.
/// </summary>
public sealed class NearbyConnectionsBuilder
{
    /// <summary>Gets the underlying service collection.</summary>
    public IServiceCollection Services { get; }

    internal NearbyConnectionsBuilder(IServiceCollection services)
        => Services = services;
}
