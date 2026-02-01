using CommunityToolkit.Mvvm.Messaging;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel
{
    public ConnectionsPageViewModel(IMessenger messenger)
        : base(messenger)
    {
    }
}