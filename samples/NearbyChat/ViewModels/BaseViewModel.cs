using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NearbyChat.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [RelayCommand]
    protected virtual void NavigatedTo() { }

    [RelayCommand]
    protected virtual void NavigatedFrom() { }
}