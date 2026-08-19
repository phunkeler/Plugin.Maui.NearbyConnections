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
            opts.AcceptTimeout = TimeSpan.FromSeconds(5);
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
        builder.Services.AddSingleton<ChatMessageStore>();
        builder.Services.AddSingleton<ConnectionTracker>();

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
