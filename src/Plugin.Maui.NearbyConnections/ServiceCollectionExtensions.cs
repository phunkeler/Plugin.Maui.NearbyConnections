using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides extension methods for adding nearby connectivity to an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INearby"/> and its configuration with the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">
    /// A delegate that configures <see cref="NearbyOptions"/>. If <see langword="null"/>, platform
    /// defaults are used — which is enough on Android, but <b>throws on iOS</b>, where
    /// <see cref="NearbyOptions.ServiceId"/> has no default and must be set. See the
    /// <see cref="ArgumentException"/> below.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance, so that multiple calls can be chained.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="INearby"/> is registered as a singleton, because the underlying resources
    /// are singular: one radio and one native session per device. It is constructed lazily, on
    /// first resolution, like any other DI singleton — nothing here forces it into existence
    /// earlier.
    /// </para>
    /// <para>
    /// <paramref name="configure"/> is applied and validated synchronously, before this method
    /// returns: an unusable <see cref="NearbyOptions.ServiceId"/> fails immediately at the call
    /// site, rather than surfacing later as a confusing failure the first time advertising starts.
    /// </para>
    /// <para>
    /// That eager validation is why <see cref="NearbyOptions"/> is not registered through
    /// <c>IOptions&lt;T&gt;</c>. The options pattern defers validation to first resolution, which on
    /// a mobile device means a misconfigured service identifier surfaces on the page that injects
    /// <see cref="INearby"/> rather than at startup. The cost of that choice is that a single
    /// <paramref name="configure"/> delegate is the only way to supply options: configuration
    /// binding and additional <c>Configure</c> calls do not apply.
    /// </para>
    /// <para>
    /// Logging must already be registered. <c>MauiAppBuilder</c> registers it, so
    /// <c>UseNearby</c> needs nothing extra. Code that builds a bare
    /// <see cref="IServiceCollection"/> calls <c>AddLogging()</c> first. A <see cref="TimeProvider"/>
    /// is registered here if the host has not registered one.
    /// </para>
    /// <para>
    /// Neither advertising nor discovery starts automatically. Call
    /// <see cref="INearby.StartAdvertisingAsync(CancellationToken)"/> or
    /// <see cref="INearby.StartDiscoveryAsync(CancellationToken)"/> once the application
    /// is ready and the required permissions have been granted.
    /// </para>
    /// <para>
    /// <see cref="INearbyDevices.Changes"/> does not replay: an app that wants to observe every
    /// connection and consume every payload from the moment it starts — rather than only from
    /// whenever a page happens to resolve <see cref="INearby"/> — registers its own
    /// <c>IMauiInitializeService</c> that resolves <see cref="INearby"/> and starts watching. That
    /// resolution constructs the singleton if it is not already alive, regardless of registration
    /// order relative to this method, because DI resolution is idempotent: whichever caller asks
    /// first gets the same instance every later caller does. See <c>NearbyIngestionService</c> in
    /// the <c>NearbyChat</c> sample for the pattern.
    /// </para>
    /// <para>
    /// The session has no UI thread affinity and takes no dispatcher: every member of
    /// <see cref="INearby"/> is callable from any thread. A consumer that binds device state to a
    /// user interface constructs a <see cref="NearbyDeviceCollection{TRow}"/>, which is where the
    /// marshalling lives.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The configured <see cref="NearbyOptions"/> is unusable. On iOS, leaving
    /// <see cref="NearbyOptions.ServiceId"/> unset always throws, because it has no iOS default;
    /// the message suggests a valid identifier derived from the application's own name. A value
    /// Multipeer Connectivity rejects also throws: null, empty, longer than 15 characters, or in
    /// the <c>_name._tcp</c> Bonjour form.
    /// </exception>
    public static IServiceCollection AddNearby(
        this IServiceCollection services,
        Action<NearbyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new NearbyOptions();
        configure?.Invoke(options);
        NearbyOptionsValidator.Validate(options);

        // Snapshot after validation: the session owns this copy, so a caller who kept the
        // configured instance cannot mutate the session's configuration past the validator.
        options = options.Snapshot();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<INearby>(sp =>
        {
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var logger = sp.GetRequiredService<ILogger<INearby>>();

            var connections = CreatePlatformNearby(timeProvider, options, logger);

            return new Nearby(connections, options, logger, timeProvider);
        });

        return services;
    }

    /// <summary>
    /// Constructs the <see cref="PlatformNearby"/> for this platform.
    /// </summary>
    /// <remarks>
    /// A partial method rather than an inline <c>#if</c> in <see cref="AddNearby"/>, so the
    /// platform/shared boundary this codebase keeps checkable via file suffix
    /// (<c>Native/PlatformNearby.*.cs</c>) extends to this registration code too.
    /// </remarks>
    private static partial PlatformNearby CreatePlatformNearby(
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger);
}