using NearbyChat.Extensions;
using NearbyChat.ViewModels;

namespace NearbyChat.Services;

public interface INavigationService
{
    Task GoToAsync<TViewModel>()
        where TViewModel : BasePageViewModel;

    Task GoBackAsync();
}

public class NavigationService : INavigationService
{
    readonly AppShell _appShell;

    public NavigationService(AppShell appShell)
    {
        ArgumentNullException.ThrowIfNull(appShell);

        _appShell = appShell;
    }

    public Task GoToAsync<TViewModel>()
        where TViewModel : BasePageViewModel
        => _appShell.GoToAsync<TViewModel>();

    public Task GoBackAsync()
        => _appShell.GoToAsync("..");
}