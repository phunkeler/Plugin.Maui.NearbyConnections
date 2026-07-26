using NearbyChat.Extensions;
using NearbyChat.ViewModels;

namespace NearbyChat.Services;

public interface INavigationService
{
    Task GoToAsync<TViewModel>()
        where TViewModel : BasePageViewModel;

    Task GoBackAsync();

    Task<string?> DisplayActionSheetAsync(string? title, string? cancel, string? destruction, params string[] buttons);
}

public class NavigationService(AppShell appShell) : INavigationService
{
    public Task GoToAsync<TViewModel>()
        where TViewModel : BasePageViewModel
        => appShell.GoToAsync<TViewModel>();

    public Task GoBackAsync()
        => appShell.GoToAsync("..");

    public Task<string?> DisplayActionSheetAsync(string? title, string? cancel, string? destruction, params string[] buttons)
        => appShell.DisplayActionSheetAsync(title, cancel, destruction, buttons);
}