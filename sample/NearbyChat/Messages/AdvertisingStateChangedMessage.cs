using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NearbyChat.Messages;

public class AdvertisingStateChangedMessage(bool value)
    : ValueChangedMessage<bool>(value);
