namespace NearbyChat.ViewModels;

public interface IViewModel
{
    bool IsBusy { get; set; }
    Task OnAppearing(object param);
}