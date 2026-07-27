using NearbyChat.Models;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Messages;

public sealed record ChatMessageReceived(NearbyDevice Device, ChatMessage Message);
