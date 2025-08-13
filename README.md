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
      <img alt="NuGet Version" src="https://img.shields.io/nuget/v/Plugin.Maui.NearbyConnections?style=for-the-badge">
    </a>
  </div>
  <div>
    <a href="https://codecov.io/gh/phunkeler/Plugin.Maui.NearbyConnections">
      <img alt="Codecov Report" src="https://img.shields.io/codecov/c/gh/phunkeler/Plugin.Maui.NearbyConnections/main?style=for-the-badge">
    </a>
  </div>
  <div>
    <a href="[CODEQL_REPORT_URL]">
        <img alt="CodeQL Report" src="[CODEQL_BADGE_URL]">
    </a>
  </div>
  <div>
    <a href="https://github.com/phunkeler/Plugin.Maui.NearbyConnections/blob/main/LICENSE">
      <img alt="GitHub License" src="https://img.shields.io/github/license/phunkeler/Plugin.Maui.NearbyConnections?style=for-the-badge">
    </a>
  </div>
  </p>
</div>

# Audience
- .NET MAUI Developers
- Open-source community

# Install
## nuget.config

 ## 📊 Performance Impact

  | Metric | Impact | Notes |
  |--------|--------|-------|
  | **App Size** | +2.1MB (Android), +800KB (iOS) | Includes native dependencies |
  | **Startup Time** | <5ms overhead | Lazy initialization |
  | **Memory** | ~200KB baseline + 50KB per connection | Efficient peer management |
  | **Battery** | Low impact | Uses platform-optimized networking |
  | **Permissions** | Location (Android), Local Network (iOS) | Required for discovery |

## Option 1:

# Acknowledgementsges

-   https://github.com/jfversluis/Plugin.Maui.Feature
-   https://github.com/puguhsudarma/expo-nearby-connections
-   https://github.com/VNAPNIC/flutter_nearby_connections
