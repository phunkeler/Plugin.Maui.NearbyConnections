using CommunityToolkit.Mvvm.Messaging.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public class DeviceStateChangedMessage(NearbyDevice value)
    : ValueChangedMessage<NearbyDevice>(value);
