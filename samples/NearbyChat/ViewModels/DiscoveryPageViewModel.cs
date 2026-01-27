using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using System.ComponentModel;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BaseViewModel
{
    readonly INearbyConnectionsService _nearbyConnectionsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    bool _isBusy;

    public bool IsDiscovering => _nearbyConnectionsService.IsDiscovering;

    public DiscoveryPageViewModel(INearbyConnectionsService nearbyConnectionsService)
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

    [RelayCommand(CanExecute = nameof(CanToggleDiscovery))]
    async Task ToggleDiscovery(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsDiscovering)
            {
                await _nearbyConnectionsService.StopDiscoveryAsync(cancellationToken);
            }
            else
            {
                await _nearbyConnectionsService.StartDiscoveryAsync(cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    bool CanToggleDiscovery() => !IsBusy;

    void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INearbyConnectionsService.IsDiscovering))
        {
            OnPropertyChanged(nameof(IsDiscovering));
        }
    }

}