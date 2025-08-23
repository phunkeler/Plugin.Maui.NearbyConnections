using CommunityToolkit.Mvvm.ComponentModel;

namespace NearbyChat.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;
}