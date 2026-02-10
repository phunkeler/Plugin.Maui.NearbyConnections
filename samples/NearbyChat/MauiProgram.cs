using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NearbyChat.Data;
using NearbyChat.Pages;
using NearbyChat.Services;
using NearbyChat.ViewModels;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("NearbyChatIcons.ttf", "NearbyChatIcons");
            })
            .UseMauiCommunityToolkit();

        builder.Services.AddNearbyConnections(options =>
        {
            options.AutoAcceptConnections = false;
        });

#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddLogging(configure =>
        {
            configure.AddDebug();
            configure.SetMinimumLevel(LogLevel.Debug);
        });
#endif

        builder.Services.AddSingleton(FileSystem.Current);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton(_ =>
        {
            return Application.Current?.Dispatcher ?? throw new InvalidOperationException("Dispatcher is not available.");
        });
        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<INearbyDeviceViewModelFactory, NearbyDeviceViewModelFactory>();
        builder.Services.AddSingleton<AvatarRepository>();
        builder.Services.AddSingleton<UserRepository>();
        builder.Services.AddSingleton<ISeedDataService, SeedDataService>();
        builder.Services.AddSingleton<IUserService, UserService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<INearbyConnectionsService, NearbyConnectionsService>();
        builder.Services.AddTransientWithShellRoute<MainPage, MainPageViewModel>();
        builder.Services.AddTransientWithShellRoute<AdvertisingPage, AdvertisingPageViewModel>();
        builder.Services.AddTransientWithShellRoute<DiscoveryPage, DiscoveryPageViewModel>();
        builder.Services.AddTransientWithShellRoute<ConnectionsPage, ConnectionsPageViewModel>();

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
