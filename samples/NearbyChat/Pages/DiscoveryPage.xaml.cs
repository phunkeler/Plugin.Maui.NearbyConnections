using System.ComponentModel;
using NearbyChat.Controls;
using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class DiscoveryPage : BasePage<DiscoveryPageViewModel>
{
    readonly Color _inactiveColor;
    readonly Color _pulseColor;

    public DiscoveryPage(DiscoveryPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();

        _inactiveColor = Application.Current!.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["DarkTextQuaternary"]
            : (Color)Application.Current.Resources["LightTextQuaternary"];
        _pulseColor = (Color)Application.Current.Resources["AccentDiscovery"];
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (ViewModel.IsDiscovering)
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
        if (e.PropertyName == nameof(DiscoveryPageViewModel.IsDiscovering))
        {
            if (ViewModel.IsDiscovering)
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
        if (SonarIcon is null || SonarIconSource is null)
        {
            return;
        }

        SonarIconSource.Color = _pulseColor;
        SonarIcon.StartPulse();
    }

    void StopPulseAnimation()
    {
        if (SonarIcon is null || SonarIconSource is null)
        {
            return;
        }

        SonarIcon.StopPulse();
        SonarIconSource.Color = _inactiveColor;
    }
}
