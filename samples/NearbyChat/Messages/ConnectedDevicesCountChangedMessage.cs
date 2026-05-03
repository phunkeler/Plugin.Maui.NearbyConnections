using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NearbyChat.Messages;

public class ConnectedDevicesCountChangedMessage(int count)
    : ValueChangedMessage<int>(count)
{

}