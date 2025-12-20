using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using NearbyChat.Data;
using NearbyChat.Pages;
using NearbyChat.Services;
using NearbyChat.ViewModels;
using Plugin.Maui.NearbyConnections;
using Syncfusion.Maui.Toolkit.Hosting;

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
            .UseMauiCommunityToolkit()
            .ConfigureSyncfusionToolkit()
            .AddNearbyConnections();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Services.AddLogging(configure =>
        {
            configure.AddDebug();
            configure.SetMinimumLevel(LogLevel.Debug);
        });
#endif

        builder.Services.AddSingleton(_ => FileSystem.Current);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<AvatarRepository>();
        builder.Services.AddSingleton<UserRepository>();
        builder.Services.AddSingleton<ISeedDataService, SeedDataService>();
        builder.Services.AddSingleton<IUserService, UserService>();

        AddTransientWithShellRoute<MainPage, MainPageViewModel>(builder.Services);

        return builder.Build();
    }

    static IServiceCollection AddTransientWithShellRoute<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel>
        (this IServiceCollection services)
        where TPage : BasePage<TViewModel>
        where TViewModel : BaseViewModel
    {
        return services.AddTransientWithShellRoute<TPage, TViewModel>(AppShell.GetPageRoute<TViewModel>());
    }
}
