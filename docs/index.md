# Plugin.Maui.NearbyConnections Documentation

Welcome to the API documentation for Plugin.Maui.NearbyConnections.

## Getting Started

Plugin.Maui.NearbyConnections is a .NET MAUI plugin that facilitates peer-to-peer (P2P) connections between nearby devices by unifying Google's [Nearby Connections](https://developers.google.com/nearby/connections/overview) and Apple's [Multipeer Connectivity](https://developer.apple.com/documentation/multipeerconnectivity) capabilities.

## Installation

Install the plugin from NuGet:

```bash
dotnet add package Plugin.Maui.NearbyConnections
```

## Quick Start

1. Add the plugin to your MAUI app's builder:

```csharp
builder
    .UseMauiApp<App>()
    .UseNearbyConnections();
```

2. Inject INearbyConnections into your pages or services:

```csharp
public class MyPage : ContentPage
{
    public MyPage(INearbyConnections nearbyConnections)
    {
        // Use nearbyConnections here
    }
}
```

## API Reference

See the [API Documentation](api/index.html) for detailed information about the available types and members.
