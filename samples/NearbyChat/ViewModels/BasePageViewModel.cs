using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(IMessenger messenger)
    : ObservableRecipient(messenger)
{
    [RelayCommand]
    protected virtual void NavigatedTo()
    {
        IsActive = true;
    }

    [RelayCommand]
    protected virtual void NavigatedFrom()
    {
        IsActive = false;
    }
}