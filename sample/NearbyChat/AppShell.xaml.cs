using System.Collections.ObjectModel;
using NearbyChat.Pages;
using NearbyChat.ViewModels;

namespace NearbyChat;

public partial class AppShell : Shell
{
    static readonly ReadOnlyDictionary<Type, Type> s_viewModelMappings = new(
        new KeyValuePair<Type, Type>[]
        {
            CreateViewModelMapping<MainPage, MainPageViewModel>(),
            CreateViewModelMapping<AdvertisingPage, AdvertisingPageViewModel>(),
            CreateViewModelMapping<DiscoveryPage, DiscoveryPageViewModel>(),
            CreateViewModelMapping<ConnectionsPage, ConnectionsPageViewModel>(),
        }.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

    public AppShell()
    {
        InitializeComponent();
    }

    public static string GetPageRoute<TViewModel>()
        where TViewModel : BasePageViewModel
    {
        var viewModelType = typeof(TViewModel);

        if (!viewModelType.IsAssignableTo(typeof(BasePageViewModel)))
        {
            throw new ArgumentException($"{nameof(viewModelType)} must implement {nameof(BasePageViewModel)}", nameof(viewModelType));
        }

        if (!s_viewModelMappings.TryGetValue(viewModelType, out var mapping))
        {
            throw new KeyNotFoundException($"No map for ${viewModelType} was found on navigation mappings. Please register your ViewModel in {nameof(AppShell)}.{nameof(s_viewModelMappings)}");
        }

        var uri = new UriBuilder("", $"//{mapping.Name}");
        return uri.Uri.OriginalString[..^1];
    }

    static KeyValuePair<Type, Type> CreateViewModelMapping<TPage, TViewModel>()
        where TPage : BasePage<TViewModel>
        where TViewModel : BasePageViewModel
        => new(typeof(TViewModel), typeof(TPage));
}
