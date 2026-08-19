using System.Collections.Frozen;
using NearbyChat.Pages;
using NearbyChat.ViewModels;

namespace NearbyChat;

public partial class AppShell : Shell
{
    static readonly FrozenDictionary<Type, Type> s_viewModelMappings = new[]
    {
        CreateViewModelMapping<MainPage, MainPageViewModel>(),
        CreateViewModelMapping<AdvertisingPage, AdvertisingPageViewModel>(),
        CreateViewModelMapping<DiscoveryPage, DiscoveryPageViewModel>(),
        CreateViewModelMapping<ConnectionsPage, ConnectionsPageViewModel>(),
    }.ToFrozenDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public AppShell()
    {
        InitializeComponent();
    }

    public static string GetPageRoute<TViewModel>()
        where TViewModel : BasePageViewModel
    {
        var viewModelType = typeof(TViewModel);

        if (!s_viewModelMappings.TryGetValue(viewModelType, out var mapping))
        {
            throw new KeyNotFoundException($"No map for {viewModelType} was found on navigation mappings. Please register your ViewModel in {nameof(AppShell)}.{nameof(s_viewModelMappings)}");
        }

        return $"//{mapping.Name}";
    }

    static KeyValuePair<Type, Type> CreateViewModelMapping<TPage, TViewModel>()
        where TPage : BasePage<TViewModel>
        where TViewModel : BasePageViewModel
        => new(typeof(TViewModel), typeof(TPage));
}
