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
            opts.ServiceId = "nearbychat";
            opts.ConnectTimeout = TimeSpan.FromSeconds(10);
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
        builder.Services.AddSingleton<ChatMessageStore>();
        builder.Services.AddSingleton<ConnectionTracker>();

        // Inbound payload ingestion. No initializer ritual: INearby.Connections replays the
        // connections still open, and an unconsumed connection buffers its payloads — a consumer
        // that starts late misses nothing. App's constructor resolves this once at startup.
        builder.Services.AddSingleton<NearbyIngestionService>();

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
