using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class AdvertisingPage : BasePage<AdvertisingPageViewModel>
{
    public AdvertisingPage(AdvertisingPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}