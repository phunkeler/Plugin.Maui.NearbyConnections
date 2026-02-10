using CommunityToolkit.Mvvm.Messaging;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel(
    IDispatcher dispatcher,
    IMessenger messenger) : BasePageViewModel(dispatcher, messenger)
{
}