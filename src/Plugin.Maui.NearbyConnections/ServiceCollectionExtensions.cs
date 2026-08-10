using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Hosting;

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
    /// An optional delegate that configures <see cref="NearbyOptions"/>. If
    /// <see langword="null"/>, platform defaults are used.
    /// </param>
    /// <returns>
    /// The same <see cref="IServiceCollection"/> instance, so that multiple calls can be chained.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="INearby"/> is registered as a singleton, because the underlying resources
    /// are singular: one radio and one native session per device. Options are configured through
    /// the <see cref="IOptions{TOptions}"/> pipeline and validated at startup.
    /// </para>
    /// <para>
    /// Neither advertising nor discovery starts automatically. Call
    /// <see cref="INearby.StartAdvertisingAsync(CancellationToken)"/> or
    /// <see cref="INearby.StartDiscoveryAsync(CancellationToken)"/> once the application
    /// is ready and the required permissions have been granted.
    /// </para>
    /// <para>
    /// The session is constructed during application startup rather than on first resolution, so
    /// that a service which starts watching at startup is running before any connection can be
    /// established. Constructing the session does not start advertising or discovery.
    /// </para>
    /// <para>
    /// The session has no UI thread affinity and takes no dispatcher: every member of
    /// <see cref="INearby"/> is callable from any thread. A consumer that binds device state to a
    /// user interface constructs a <see cref="NearbyDeviceCollection"/>, which is where the
    /// marshalling lives.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddNearby(
        this IServiceCollection services,
        Action<NearbyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddOptions<NearbyOptions>().ValidateOnStart();
        services.AddSingleton<IValidateOptions<NearbyOptions>, NearbyOptionsValidator>();

        services.TryAddSingleton<INearby>(sp =>
        {
            var resolvedOptions = sp.GetRequiredService<IOptions<NearbyOptions>>().Value;
            var timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
            var logger = sp.GetService<ILogger<PlatformNearby>>()
                ?? NullLogger<PlatformNearby>.Instance;

            var connections = CreatePlatformNearby(sp, timeProvider, resolvedOptions, logger);

            return new NearbyImplementation(
                connections,
                resolvedOptions,
                sp.GetService<ILogger<NearbyImplementation>>() ?? NullLogger<NearbyImplementation>.Instance);
        });

        // Forces the session into existence during MauiAppBuilder.Build(), rather than leaving it
        // to whichever consumer happens to resolve it first. See NearbySessionInitializer for the
        // silent payload loss this prevents. TryAddEnumerable because MAUI runs these via
        // GetServices<T>(): a second AddNearby() call would otherwise register a
        // duplicate initializer.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMauiInitializeService, NearbySessionInitializer>());

        return services;
    }

    /// <summary>
    /// Constructs the <see cref="PlatformNearby"/> for this platform, resolving whatever
    /// platform-specific dependencies its constructor needs from <paramref name="services"/>.
    /// </summary>
    /// <remarks>
    /// A partial method rather than an inline <c>#if</c> in <see cref="AddNearby"/>, so the
    /// platform/shared boundary this codebase keeps checkable via file suffix
    /// (<c>Native/PlatformNearby.*.cs</c>) extends to this registration code too.
    /// </remarks>
    private static partial PlatformNearby CreatePlatformNearby(
        IServiceProvider services,
        TimeProvider timeProvider,
        NearbyOptions options,
        ILogger logger);

    /// <summary>
    /// Constructs <see cref="INearby"/> during <c>MauiAppBuilder.Build()</c> so the session
    /// — and any consumer watching it at startup — is alive before the first connection can be
    /// established.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="INearbyDevices.Changes"/> does not replay. The container creates singletons
    /// lazily, on first resolution, so without this the session might not exist until a page
    /// injected it — and a connection established before that point is a transition nobody
    /// observed. Inbound payloads are then written to a channel with no reader: no exception, no
    /// log, messages simply never arrive. A watcher that starts late can recover the current state
    /// from <see cref="INearby.Devices"/>, but only if it knows to look.
    /// </para>
    /// <para>
    /// Resolving the session is the entire job; the resolved instance is deliberately discarded
    /// because the container owns it from here on.
    /// </para>
    /// </remarks>
    sealed class NearbySessionInitializer : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
            => services.GetRequiredService<INearby>();
    }
}
