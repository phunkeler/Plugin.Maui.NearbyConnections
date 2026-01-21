using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;


namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BaseViewModel
{
    readonly INearbyConnectionsService _nearbyConnectionsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    bool _isBusy;

    public AdvertisingPageViewModel(INearbyConnectionsService nearbyConnectionsService)
    {
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _nearbyConnectionsService = nearbyConnectionsService;
    }

    protected override void NavigatedTo()
    {
        _nearbyConnectionsService.PropertyChanged += OnServicePropertyChanged;
    }

    protected override void NavigatedFrom()
    {
        _nearbyConnectionsService.PropertyChanged -= OnServicePropertyChanged;
    }

    public bool IsAdvertising => _nearbyConnectionsService.IsAdvertising;

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

    void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INearbyConnectionsService.IsAdvertising))
        {
            OnPropertyChanged(nameof(IsAdvertising));
        }
    }
}
