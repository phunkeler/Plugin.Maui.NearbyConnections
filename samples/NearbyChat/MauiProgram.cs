using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Maui;
using Indiko.Maui.Controls.Chat;
using NearbyChat.Pages;
using NearbyChat.ViewModels;

namespace NearbyChat;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseChatView();


        AddTransientWithShellRoute<MainPage, MainPageViewModel>(builder.Services);
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
