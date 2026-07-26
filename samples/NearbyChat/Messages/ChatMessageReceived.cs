using NearbyChat.Models;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.Messages;

public sealed record ChatMessageReceived(NearbyDevice Device, ChatMessage Message);
