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
            // AutoAcceptConnectionRequests stays off deliberately: this sample demonstrates the
            // prompt flow, and setting it true would accept every inbound request from any device
            // that knows the service id, skip NearbyDeviceStatus.RequestReceived entirely, and make
            // AdvertisingPageViewModel and AdvertisedDeviceViewModel dead code.
            opts.ServiceId = "nearbychat";

            // Shortened from the 30s default so a failed handshake surfaces quickly in a demo. The
            // accept window is shortened with it: leaving AcceptTimeout at its default would make
            // accepting wait longer than connecting, inverting the relationship the defaults
            // express — a connect includes the remote user's decision, an accept does not.
            opts.ConnectTimeout = TimeSpan.FromSeconds(10);
            opts.AcceptTimeout = TimeSpan.FromSeconds(5);

            // An unanswered prompt is withdrawn after this, and the row disappears.
            // NearbyDevice.RequestExpiresAt carries the deadline for a countdown display.
            opts.InboundRequestTimeout = TimeSpan.FromSeconds(30);
        });

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddFilter("Plugin.Maui.NearbyConnections", LogLevel.Trace);
#endif

        builder.Services.AddSingleton(MediaPicker.Default);
        builder.Services.AddSingleton(Launcher.Default);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IThumbnailService, ThumbnailService>();
        builder.Services.AddSingleton<INearbyPermissions, NearbyPermissions>();
        builder.Services.AddSingleton<ChatMessageStore>();
        builder.Services.AddSingleton<IChatMessageRepository, ChatMessageRepository>();
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
