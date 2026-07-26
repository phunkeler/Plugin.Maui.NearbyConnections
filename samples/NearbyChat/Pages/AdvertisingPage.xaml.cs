using System.ComponentModel;
using NearbyChat.Controls;
using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class AdvertisingPage : BasePage<AdvertisingPageViewModel>
{
    readonly Color _inactiveColor;
    readonly Color _pulseColor;

    public AdvertisingPage(AdvertisingPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();

        _inactiveColor = Application.Current!.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["DarkTextQuaternary"]
            : (Color)Application.Current.Resources["LightTextQuaternary"];
        _pulseColor = (Color)Application.Current.Resources["AccentAdvertising"];
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (ViewModel.IsAdvertising)
        {
            StartPulseAnimation();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        StopPulseAnimation();
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvertisingPageViewModel.IsAdvertising))
        {
            if (ViewModel.IsAdvertising)
            {
                StartPulseAnimation();
            }
            else
            {
                StopPulseAnimation();
            }
        }
    }

    void StartPulseAnimation()
    {
        if (AntennaIcon is null || AntennaIconSource is null)
        {
            return;
        }

        AntennaIconSource.Color = _pulseColor;
        AntennaIcon.StartPulse();
    }

    void StopPulseAnimation()
    {
        if (AntennaIcon is null || AntennaIconSource is null)
        {
            return;
        }

        AntennaIcon.StopPulse();
        AntennaIconSource.Color = _inactiveColor;
    }
}
