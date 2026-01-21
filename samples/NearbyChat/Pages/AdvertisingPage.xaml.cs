using NearbyChat.ViewModels;
using System.ComponentModel;

namespace NearbyChat.Pages;

public partial class AdvertisingPage : BasePage<AdvertisingPageViewModel>, IDisposable
{
    readonly Color _inactiveColor;
    readonly Color _pulseColor;
    CancellationTokenSource? _animationCts;

    public AdvertisingPage(AdvertisingPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();

        _inactiveColor = Application.Current!.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["DarkTextQuaternary"]
            : (Color)Application.Current.Resources["LightTextQuaternary"];
        _pulseColor = (Color)Application.Current.Resources["AccentAdvertising"];

        BindingContext.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext.IsAdvertising)
        {
            StartPulseAnimation();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        StopPulseAnimation();
    }

    public void Dispose()
    {
        BindingContext.PropertyChanged -= OnViewModelPropertyChanged;
        _animationCts?.Cancel();
        _animationCts?.Dispose();

        GC.SuppressFinalize(this);
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvertisingPageViewModel.IsAdvertising))
        {
            if (BindingContext.IsAdvertising)
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

        if (AntennaIcon is null || AntennaIconSource is null)
            return;

        AntennaIcon.CancelAnimations();
        AntennaIconSource.Color = _inactiveColor;
        AntennaIcon.Opacity = 1;
    }

    async Task RunPulseAnimationAsync(CancellationToken cancellationToken)
    {
        if (AntennaIcon is null || AntennaIconSource is null)
            return;

        AntennaIconSource.Color = _pulseColor;

        while (!cancellationToken.IsCancellationRequested)
        {
            await AntennaIcon.FadeToAsync(0.4, 800, Easing.CubicInOut);

            if (cancellationToken.IsCancellationRequested) break;

            await AntennaIcon.FadeToAsync(1, 800, Easing.CubicInOut);
        }
    }
}