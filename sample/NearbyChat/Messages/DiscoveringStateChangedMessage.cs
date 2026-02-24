using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NearbyChat.Messages;

public class DiscoveringStateChangedMessage(bool value)
    : ValueChangedMessage<bool>(value);
