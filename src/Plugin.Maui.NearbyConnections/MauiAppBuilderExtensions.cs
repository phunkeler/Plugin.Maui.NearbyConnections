using Microsoft.Extensions.Options;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides the <see cref="MauiAppBuilder"/> entry point for registering nearby connectivity.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="INearby"/> and its configuration with the application builder.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to register the services with.</param>
    /// <param name="configure">
    /// An optional delegate that configures <see cref="NearbyOptions"/>. If
    /// <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>
    /// The same <see cref="MauiAppBuilder"/> instance, so that calls can be chained.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="INearby"/> is registered as a singleton — resolve it through constructor
    /// injection wherever nearby connectivity is needed.
    /// </para>
    /// <para>
    /// Neither advertising nor discovery starts automatically. Call
    /// <see cref="INearby.StartAdvertisingAsync(CancellationToken)"/> or
    /// <see cref="INearby.StartDiscoveryAsync(CancellationToken)"/> once the application is ready
    /// and the required permissions have been granted.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OptionsValidationException">
    /// The configured <see cref="NearbyOptions"/> is unusable — for example,
    /// <see cref="NearbyOptions.ServiceId"/> is null, empty, or not valid for Multipeer
    /// Connectivity.
    /// </exception>
    /// <example>
    /// The following example registers the plugin and sets the service identifier.
    /// <code language="csharp">
    /// builder.UseNearby(options =>
    /// {
    ///     options.ServiceId = "nearbychat";
    /// });
    /// </code>
    /// </example>
    public static MauiAppBuilder UseNearby(
        this MauiAppBuilder builder,
        Action<NearbyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddNearby(configure);

        return builder;
    }
}
