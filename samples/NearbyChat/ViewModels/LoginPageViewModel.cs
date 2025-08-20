
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Resources.Themes;

namespace NearbyChat.ViewModels;

public partial class LoginPageViewModel : BaseViewModel
{
    public override Task OnAppearing(object param) => Task.CompletedTask;

    [RelayCommand]
#pragma warning disable CA1822 // Mark members as static
    void ToggleTheme()
#pragma warning restore CA1822 // Mark members as static
    {
        var mergedDictionaries = Application.Current?.Resources.MergedDictionaries;

        if (mergedDictionaries is not null)
        {
            mergedDictionaries.Clear();
            mergedDictionaries.Add(new DarkTheme());
        }
    }
}