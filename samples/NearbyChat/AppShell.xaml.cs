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
            CreateViewModelMapping<ChatPage, ChatPageViewModel>()
        }.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

    public AppShell()
    {
        InitializeComponent();
    }

    public static string GetPageRoute<TViewModel>() where TViewModel : BaseViewModel
    {
        var viewModelType = typeof(TViewModel);

        if (!viewModelType.IsAssignableTo(typeof(BaseViewModel)))
        {
            throw new ArgumentException($@"{nameof(viewModelType)} must implement {nameof(BaseViewModel)}", nameof(viewModelType));
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
        where TViewModel : BaseViewModel
        => new(typeof(TViewModel), typeof(TPage));
}
