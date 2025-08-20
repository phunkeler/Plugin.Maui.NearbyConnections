using CommunityToolkit.Mvvm.Input;

namespace NearbyChat.ViewModels;

public partial class LoginPageViewModel : BaseViewModel
{
    [RelayCommand]
    private async Task Login()
    {
        // Navigate to the main chat page after login
        await Shell.Current.GoToAsync("//ChatPage");
    }

    public override Task OnAppearing(object param) => Task.CompletedTask;

}