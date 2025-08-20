using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public abstract class BasePage<TViewModel>(TViewModel viewModel) : BasePage(viewModel)
    where TViewModel : BaseViewModel
{
    public new TViewModel BindingContext => (TViewModel)base.BindingContext;
}

public abstract class BasePage : ContentPage
{
    protected BasePage(object? viewModel = null)
    {
        BindingContext = viewModel;

        if (string.IsNullOrWhiteSpace(Title))
        {
            Title = GetType().Name;
        }
    }
}