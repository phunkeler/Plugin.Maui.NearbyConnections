using CommunityToolkit.Mvvm.Input;
using NearbyChat.Pages;

namespace NearbyChat.ViewModels;

public partial class LoginPageViewModel : BaseViewModel
{
    [RelayCommand]
    private async Task Login() =>
        await Shell.Current.GoToAsync($"//{nameof(ChatPage)}");

    public override Task OnAppearing(object param) => Task.CompletedTask;

}