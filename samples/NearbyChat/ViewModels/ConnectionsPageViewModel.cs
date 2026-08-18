using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _nearby;
    readonly IBottomSheetNavigationService _bottomSheetNavigationService;

    public ConnectionsPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby nearby,
        IBottomSheetNavigationService bottomSheetNavigationService)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(bottomSheetNavigationService);

        _navigationService = navigationService;
        _nearby = nearby;
        _bottomSheetNavigationService = bottomSheetNavigationService;
    }

    /// <summary>
    /// The devices this app is currently connected to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plugin's own bindable projection, matching the other two device pages. It replaces the
    /// watch loop, the seed pass, and the add/remove bookkeeping this page used to carry: a device
    /// that leaves <see cref="NearbyDeviceStatus.Connected"/> stops matching the filter and its row
    /// is dropped.
    /// </para>
    /// <para>
    /// Built in <see cref="NavigatedTo"/> and disposed in <see cref="NavigatedFrom"/>, so it lives
    /// exactly as long as the page is on screen.
    /// </para>
    /// </remarks>
    public NearbyDeviceCollection<ConnectedDeviceViewModel>? ConnectedDevices { get; private set; }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        // Seeding is free: the constructor reads the current device set before it starts watching,
        // so connections made while the page was away are already in the new collection.
        ConnectedDevices = new NearbyDeviceCollection<ConnectedDeviceViewModel>(
            _nearby,
            action => Dispatcher.Dispatch(action),
            project: device => new ConnectedDeviceViewModel(device, _nearby, _bottomSheetNavigationService),
            filter: static device => device.Status is NearbyDeviceStatus.Connected,
            update: static (row, device) => row.Update(device));

        OnPropertyChanged(nameof(ConnectedDevices));
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();

        if (ConnectedDevices is { } devices)
        {
            devices.Dispose();

            ConnectedDevices = null;
            OnPropertyChanged(nameof(ConnectedDevices));
        }
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();
}
