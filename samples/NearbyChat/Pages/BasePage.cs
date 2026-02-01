using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public abstract class BasePage<TViewModel>(TViewModel viewModel) : BasePage(viewModel)
    where TViewModel : BasePageViewModel
{
    public new TViewModel BindingContext => (TViewModel)base.BindingContext;

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        BindingContext.NavigatedToCommand.Execute(null);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        BindingContext.NavigatedFromCommand.Execute(null);
    }
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