using Plugin.Maui.NearbyDevices;

namespace NearbyChat.Messages;

public sealed record InboundTransferProgress(NearbyDevice Device, NearbyTransferProgress Progress);
