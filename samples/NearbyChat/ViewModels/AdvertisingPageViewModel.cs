using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel,
    IRecipient<AdvertisingStateChangedMessage>
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public AdvertisingPageViewModel(
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;

        IsAdvertising = _nearbyConnectionsService.IsAdvertising;
    }

    public void Receive(AdvertisingStateChangedMessage message)
    {
        IsAdvertising = message.Value;
    }

    [RelayCommand]
    async Task Back()
    {
        await _navigationService.GoBackAsync();
    }

    [RelayCommand(CanExecute = nameof(CanToggleAdvertising))]
    async Task ToggleAdvertising(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsAdvertising)
            {
                await _nearbyConnectionsService.StopAdvertisingAsync(cancellationToken);
            }
            else
            {
                await _nearbyConnectionsService.StartAdvertisingAsync(cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    bool CanToggleAdvertising() => !IsBusy;
}
