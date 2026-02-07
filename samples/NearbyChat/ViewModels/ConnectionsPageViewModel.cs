using CommunityToolkit.Mvvm.Messaging;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel
{
    public ConnectionsPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger)
        : base(dispatcher, messenger)
    {
    }
}