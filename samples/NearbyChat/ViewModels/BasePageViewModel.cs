using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(
    IDispatcher dispatcher,
    IMessenger messenger)
    : ObservableRecipient(messenger)
{
    protected IDispatcher Dispatcher { get; } = dispatcher;

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