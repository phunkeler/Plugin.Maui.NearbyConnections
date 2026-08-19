using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _nearby;

    public MainPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby nearby,
        ConnectionTracker connectionTracker)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(connectionTracker);

        _navigationService = navigationService;
        _nearby = nearby;
        Connections = connectionTracker;
    }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public ConnectionTracker Connections { get; }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        _ = WatchAsync(_nearby.AdvertisingChanges, value => IsAdvertising = value, NavigationToken);
        _ = WatchAsync(_nearby.DiscoveryChanges, value => IsDiscovering = value, NavigationToken);

        IsAdvertising = _nearby.IsAdvertising;
        IsDiscovering = _nearby.IsDiscovering;
    }

    /// <summary>
    /// Keeps one header indicator honest for as long as the page is on screen. The platform stops
    /// advertising and discovery on its own — backgrounding, a radio fault — so a value read once
    /// on navigation goes stale with nothing to correct it.
    /// </summary>
    async Task WatchAsync(IAsyncEnumerable<bool> changes, Action<bool> apply, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var value in changes.WithCancellation(cancellationToken))
            {
                await Dispatcher.DispatchAsync(() => apply(value));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Navigated away. Ending the loop is the only cleanup.
        }
    }

    [RelayCommand]
    Task NavigateToAdvertising()
        => _navigationService.GoToAsync<AdvertisingPageViewModel>();

    [RelayCommand]
    Task NavigateToDiscovery()
        => _navigationService.GoToAsync<DiscoveryPageViewModel>();

    [RelayCommand]
    Task NavigateToConnections()
        => _navigationService.GoToAsync<ConnectionsPageViewModel>();
}
