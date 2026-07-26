using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public abstract class BasePage<TViewModel> : ContentPage
    where TViewModel : BasePageViewModel
{
    protected BasePage(TViewModel viewModel)
    {
        BindingContext = viewModel;
    }

    protected TViewModel ViewModel => (TViewModel)BindingContext;

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        ViewModel.NavigatedToCommand.Execute(null);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        ViewModel.NavigatedFromCommand.Execute(null);
        base.OnNavigatedFrom(args);
    }
}
