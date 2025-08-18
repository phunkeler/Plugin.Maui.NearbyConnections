using CommunityToolkit.Mvvm.ComponentModel;

namespace NearbyChat.ViewModels;

public abstract partial class BaseViewModel : ObservableObject, IViewModel
{
    [ObservableProperty]
    private bool _isBusy;

    public abstract Task OnAppearing(object param);
}