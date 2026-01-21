<div align="center">
  <picture>
    <img src=".assets/nuget.svg" width="180">
  </picture>

  <h1>
    Plugin.Maui.NearbyConnections
  </h1>
  <p>
    A .NET MAUI plugin for peer-to-peer (P2P) connectivity with nearby devices by unifying Google's <a href="https://developers.google.com/nearby/connections/overview" target="_blank">Nearby Connections</a> and Apple's <a href="https://developer.apple.com/documentation/multipeerconnectivity" target="_blank">Multipeer Connectivity</a> capabilities.
  </p>
</div>
<h1>
</h1>
</br>

<div align="center">
  <div>
    <a href="https://www.nuget.org/packages/Plugin.Maui.NearbyConnections">
      <img alt="NuGet Version" src="https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections">
    </a>
  </div>
  <div>
    <a href="https://codecov.io/gh/phunkeler/Plugin.Maui.NearbyConnections">
      <img alt="Codecov Report" src="https://img.shields.io/codecov/c/gh/phunkeler/Plugin.Maui.NearbyConnections/main?">
    </a>
  </div>
  <div>
    <a href="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/actions/workflows/codeql.yml">
        <img alt="CodeQL Report" src="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/actions/workflows/codeql.yml/badge.svg">
    </a>
  </div>
  <div>
    <a href="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE">
      <img alt="GitHub License" src="https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections">
    </a>
  </div>
  </p>
</div>

# Supported Platforms

| Platform | Minimum Version Supported |
| -------- | ------------------------- |
| iOS      | Not set in project.       |
| Android  | Not set in project        |
|          |                           |

# Dependencies

| Dependency | iOS | Android | .NET |
| --- | --- | --- | --- |
| [Microsoft.Maui.Core](https://www.nuget.org/packages/Microsoft.Maui.Core) | ✓ | ✓ | ✓ |
| [Xamarin.GooglePlayServices.Nearby](https://www.nuget.org/packages/Xamarin.GooglePlayServices.Nearby/) | | ✓ | |

# Getting Started
`Plugin.Maui.NearbyConnections` is ~~available~~ on [nuget.org](https://www.nuget.org/packages/Plugin.Maui.NearbyConnections)

## **dotnet**

```bash
dotnet add package Plugin.Maui.NearbyConnections -s https://api.nuget.org/v3/index.json
```

</details>

# Usage

## MauiProgram.cs

```csharp
public static MauiApp CreateMauiApp()
    => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .AddNearbyConnections() // Defaults
        .Build();

```

```csharp
public static MauiApp CreateMauiApp()
    => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .AddNearbyConnections(options => // Custom Config
        {
            options.ServiceName = "MyService";
            options.AutoAcceptConnections = false;
        })
        .Build();

```

# Acknowledgements

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections
