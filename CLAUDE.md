# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET MAUI plugin that provides peer-to-peer (P2P) connectivity with nearby devices by unifying Google's Nearby Connections (Android) and Apple's Multipeer Connectivity (iOS) capabilities. The project is in early development phase following an MVP-first approach.

## Build System

- **Framework**: .NET 9.0 (`global.json:3`)
- **Project Type**: Multi-targeted .NET MAUI plugin
- **Target Frameworks**: `net9.0`, `net9.0-android`, `net9.0-ios` (`Directory.Build.props:3-8`)
- **Solution File**: `Plugin.Maui.NearbyConnections.slnx` (Visual Studio solution)

### Build Commands

```bash
# Build the project
dotnet build

# Pack for NuGet
dotnet pack

# Build for specific platform
dotnet build -f net9.0-android
dotnet build -f net9.0-ios
```

## Architecture

### Platform-Specific Implementation Pattern

The project uses a platform-specific partial class pattern:

- **Interface**: `INearbyConnections.cs` - defines the plugin API
- **Static Entry Point**: `NearbyConnections.cs` - provides static access via `NearbyConnections.Current`
- **Base Implementation**: `NearbyConnectionsImplementation` (partial class)
  - `NearbyConnections.android.cs` - Android implementation using Google Nearby Connections
  - `NearbyConnections.ios.cs` - iOS implementation using Multipeer Connectivity
  - `NearbyConnections.net.cs` - Generic .NET implementation (throws NotImplementedException)

### Platform Dependencies

- **Android**: `Xamarin.GooglePlayServices.Nearby` package (`Directory.Packages.props:5`)
- **iOS**: Uses native `MultipeerConnectivity` framework (`NearbyConnectionsManager.ios.cs:2`)

### Build Configuration

The project uses sophisticated conditional compilation:

- **Platform Detection**: `IsTargetPlatformAndroid` and `IsTargetPlatformIos` properties (`Directory.Build.props:10-13`)
- **File Exclusion**: Platform-specific files are excluded from non-matching builds (`Directory.Build.targets:10-29`)
- **Global Usings**: Android Google Play Services namespaces are auto-imported for Android builds (`Directory.Build.targets:33-43`)

## Development Standards

- **Nullable Reference Types**: Enabled (`Directory.Build.props:15`)
- **Code Analysis**: Latest recommended level with warnings as errors (`Directory.Build.props:19-22`)
- **Documentation**: XML documentation required (`Directory.Build.props:8`)

## Current Implementation Status

The project is in MVP phase with minimal interface:
- Single method: `StartDiscoveryAsync()` (`INearbyConnections.cs:11`)
- All platform implementations currently throw `NotImplementedException`
- iOS helper class `NearbyConnectionsManager` has peer ID management foundation (`NearbyConnectionsManager.ios.cs:59-107`)

## iOS Configuration Requirements

Apps using this plugin need these Info.plist entries (`NearbyConnectionsManager.ios.cs:15-26`):

```xml
<key>NSBonjourServices</key>
<array>
  <string>_[YOURSERVICETYPE-HERE]._tcp</string>
  <string>_[YOURSERVICETYPE-HERE]._udp</string>
</array>
<key>NSLocalNetworkUsageDescription</key>
<string>[YOURDESCRIPTION-HERE]</string>
```

## Project Strategy

This project follows an "Anti-Detail-Trap Strategy" focusing on shipping early and iterating fast (`docs/PROJECTPLAN.md:3-6`). The 12-week plan prioritizes:
1. MVP Foundation (Weeks 1-4) - basic plugin + first NuGet publish
2. Core Features (Weeks 5-8) - production-ready functionality
3. Polish & Growth (Weeks 9-12) - community adoption

Priority is getting a working NuGet package published rather than building comprehensive features initially.