using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Maui;
using Indiko.Maui.Controls.Chat;
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
            .UseMauiCommunityToolkit()
            .UseChatView()
            .ConfigureSyncfusionToolkit()
            .AddNearbyConnections()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUi.FontFamily);
            });

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IChatMessageService, ChatMessageService>();
        builder.Services.AddSingleton<AvatarRepository>();
        builder.Services.AddSingleton<UserRepository>();
        builder.Services.AddSingleton<ISeedDataService, SeedDataService>();
        AddTransientWithShellRoute<LoginPage, LoginPageViewModel>(builder.Services);
        AddTransientWithShellRoute<ChatPage, ChatPageViewModel>(builder.Services);

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
