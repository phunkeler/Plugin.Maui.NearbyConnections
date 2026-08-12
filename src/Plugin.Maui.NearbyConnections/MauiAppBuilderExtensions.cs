using Microsoft.Extensions.Options;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides extension methods for adding nearby connectivity to a <see cref="MauiAppBuilder"/>.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="INearby"/> and its configuration with the application builder.
    /// </summary>
    /// <param name="builder">The <see cref="MauiAppBuilder"/> to add the services to.</param>
    /// <param name="configure">
    /// An optional delegate that configures <see cref="NearbyOptions"/>. If
    /// <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>
    /// The same <see cref="MauiAppBuilder"/> instance, so that multiple calls can be chained.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method registers <see cref="INearby"/> as a singleton. Resolve it through
    /// constructor injection wherever nearby connectivity is needed.
    /// </para>
    /// <para>
    /// Neither advertising nor discovery starts automatically. Call
    /// <see cref="INearby.StartAdvertisingAsync(CancellationToken)"/> or
    /// <see cref="INearby.StartDiscoveryAsync(CancellationToken)"/> once the application
    /// is ready and the required permissions have been granted.
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
