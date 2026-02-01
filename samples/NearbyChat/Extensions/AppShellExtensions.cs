using NearbyChat.ViewModels;

namespace NearbyChat.Extensions;

public static class AppShellExtensions
{
    public static Task GoToAsync<TViewModel>(this AppShell appShell)
        where TViewModel : BasePageViewModel
    {
        var route = AppShell.GetPageRoute<TViewModel>();
        return appShell.GoToAsync(route);
    }
}