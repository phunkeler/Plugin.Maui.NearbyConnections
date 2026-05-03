using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class ConnectionsPage : BasePage<ConnectionsPageViewModel>
{
    public ConnectionsPage(ConnectionsPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}