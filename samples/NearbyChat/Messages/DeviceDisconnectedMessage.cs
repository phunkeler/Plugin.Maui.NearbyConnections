using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class DeviceDisconnectedMessage(NearbyDevice value)
    : ValueChangedMessage<NearbyDevice>(value);
