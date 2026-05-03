using System.ComponentModel;
using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public partial class DiscoveryPage : BasePage<DiscoveryPageViewModel>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    readonly Color _inactiveColor;
    readonly Color _pulseColor;
    CancellationTokenSource? _animationCts;

    public DiscoveryPage(DiscoveryPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();

        _inactiveColor = Application.Current!.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["DarkTextQuaternary"]
            : (Color)Application.Current.Resources["LightTextQuaternary"];
        _pulseColor = (Color)Application.Current.Resources["AccentDiscovery"];

        BindingContext.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext.IsDiscovering)
        {
            StartPulseAnimation();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        StopPulseAnimation();
        BindingContext.PropertyChanged -= OnViewModelPropertyChanged;
        _animationCts?.Dispose();
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscoveryPageViewModel.IsDiscovering))
        {
            if (BindingContext.IsDiscovering)
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
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();

        _ = RunPulseAnimationAsync(_animationCts.Token);
    }

    void StopPulseAnimation()
    {
        _animationCts?.Cancel();
        _animationCts = null;

        if (SonarIcon is null || SonarIconSource is null)
            return;

        SonarIcon.CancelAnimations();
        SonarIcon.Opacity = 1;
        SonarIconSource.Color = _inactiveColor;
    }

    async Task RunPulseAnimationAsync(CancellationToken cancellationToken)
    {
        if (SonarIcon is null || SonarIconSource is null)
            return;

        SonarIconSource.Color = _pulseColor;

        while (!cancellationToken.IsCancellationRequested)
        {
            await SonarIcon.FadeToAsync(0.4, 800, Easing.CubicInOut);

            if (cancellationToken.IsCancellationRequested) break;

            await SonarIcon.FadeToAsync(1, 800, Easing.CubicInOut);
        }
    }
}