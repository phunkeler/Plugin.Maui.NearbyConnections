using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public sealed record InboundTransferProgress(NearbyDevice Device, NearbyTransferProgress Progress);
