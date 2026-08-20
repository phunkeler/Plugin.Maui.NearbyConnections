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
    /// A delegate that configures <see cref="NearbyOptions"/>. If <see langword="null"/>, platform
    /// defaults are used — which is sufficient on Android, but <b>throws on iOS</b>, where
    /// <see cref="NearbyOptions.ServiceId"/> has no default and must be set. See the
    /// <see cref="ArgumentException"/> below.
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
    /// <exception cref="ArgumentException">
    /// The configured <see cref="NearbyOptions"/> is unusable. On iOS, leaving
    /// <see cref="NearbyOptions.ServiceId"/> unset always throws, because it has no iOS default;
    /// the message suggests a valid identifier derived from the application's own name. A value
    /// Multipeer Connectivity rejects also throws: null, empty, longer than 15 characters, or in
    /// the <c>_name._tcp</c> Bonjour form.
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