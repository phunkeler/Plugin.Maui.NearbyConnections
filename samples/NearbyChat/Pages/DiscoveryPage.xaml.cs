using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class DiscoveryPage : BasePage<DiscoveryPageViewModel>
{
    public DiscoveryPage(DiscoveryPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}