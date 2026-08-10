using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using NearbyChat.Controls;
using NearbyChat.Data;
using NearbyChat.Pages;
using NearbyChat.Services;
using NearbyChat.ViewModels;
using Plugin.Maui.BottomSheet.Hosting;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
#if DEBUG
            .AddMauiDevFlowAgent()
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("NearbyChatIcons.ttf", "NearbyChatIcons");
            })
            .UseMauiCommunityToolkit()
            .UseBottomSheet();

        builder.UseNearby(opts =>
        {
            // AutoAcceptConnectionRequests is deliberately left off. This sample prompts, because
            // that is the flow worth demonstrating: AdvertisingPageViewModel surfaces the request
            // and AdvertisedDeviceViewModel answers it. Setting it to true would accept every
            // inbound request from any device that knows the service id, skip
            // NearbyDeviceStatus.RequestReceived entirely, and make both of those types dead code.
#if IOS
            opts.ServiceId = "nearbychat";
            opts.InvitationTimeout = TimeSpan.FromSeconds(10);
#endif
        });

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddFilter("Plugin.Maui.NearbyConnections", LogLevel.Trace);
#endif

        builder.Services.AddSingleton(MediaPicker.Default);
        builder.Services.AddSingleton(Launcher.Default);
        builder.Services.AddSingleton<AppShell>();

        // IDispatcher is deliberately NOT registered here. MAUI already registers it, and the
        // previous `Application.Current?.Dispatcher ?? throw` factory made resolution depend on
        // *when* it ran: Application.Current is still null during MauiAppBuilder.Build(), so
        // anything resolved at startup — such as an IMauiInitializeService — crashed the app with
        // "Dispatcher is not available." rather than getting the perfectly good dispatcher MAUI
        // provides.
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IThumbnailService, ThumbnailService>();
        builder.Services.AddSingleton<INearbyPermissions, NearbyPermissions>();
        // Persistence. The store is the singleton that outlives any unit of work (a database, in a
        // real app); the repository is Scoped and reached only through the factory, so no long-lived
        // service can capture one. See IChatMessageRepositoryFactory.
        builder.Services.AddSingleton<ChatMessageStore>();
        builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        builder.Services.AddSingleton<IChatMessageRepositoryFactory, ChatMessageRepositoryFactory>();

        builder.Services.AddSingleton<IChatMessageService, ChatMessageService>();
        builder.Services.AddSingleton<IConnectionTracker, ConnectionTracker>();

        // Inbound payload ingestion must be running before the first connection is established,
        // because ConnectionEstablished does not replay. IMauiInitializeService runs during
        // Build(), so being attached in time is a property of the type rather than a side effect of
        // who resolves it. TryAddEnumerable because MAUI invokes these via GetServices<T>() and a
        // duplicate registration would double every inbound message.
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMauiInitializeService, NearbyIngestionService>());

        builder.Services.AddTransientWithShellRoute<AdvertisingPage, AdvertisingPageViewModel>();
        builder.Services.AddTransientWithShellRoute<ConnectionsPage, ConnectionsPageViewModel>();
        builder.Services.AddTransientWithShellRoute<DiscoveryPage, DiscoveryPageViewModel>();
        builder.Services.AddTransientWithShellRoute<MainPage, MainPageViewModel>();

        builder.Services.AddBottomSheet<ChatBottomSheet, ChatViewModel>(nameof(ChatViewModel));


        return builder.Build();
    }

    static IServiceCollection AddTransientWithShellRoute<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel>
        (this IServiceCollection services)
        where TPage : BasePage<TViewModel>
        where TViewModel : BasePageViewModel
    {
        return services.AddTransientWithShellRoute<TPage, TViewModel>(AppShell.GetPageRoute<TViewModel>());
    }
}
