# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET MAUI plugin that provides peer-to-peer (P2P) connectivity with nearby devices by unifying Google's Nearby Connections (Android) and Apple's Multipeer Connectivity (iOS) capabilities. The project is in early development phase following an MVP-first approach.

## Tech Stack
- .NET 10 / C# (modern style)
- .NET MAUI (Minimal APIs)

## Build System
- **Project Type**: Multi-targeted .NET MAUI plugin
- **Target Frameworks**: `net10.0`, `net10.0-android`, `net10.0-ios` (`Directory.Build.props:3-8`)
- **Solution File**: `Plugin.Maui.NearbyConnections.slnx` (Visual Studio solution)

### Build Commands

```bash
# Build the project
dotnet build

# Pack for NuGet
dotnet pack

# Build for specific platform
dotnet build -f net10.0-android
dotnet build -f net10.0-ios
```

## Architecture

### Platform-Specific Implementation Pattern

The project uses a platform-specific partial class pattern across a single sealed class `NearbyConnectionsImplementation`:

- **Interface**: `INearbyConnections.cs` — defines the full public API
- **Shared logic**: `NearbyConnections.shared.cs` — DI constructor, semaphore-guarded start/stop, send/disconnect dispatch
- **Platform partials**:
  - `NearbyConnections.android.cs` — Android advertising, discovery, and data transfer via Google Nearby Connections
  - `NearbyConnections.ios.cs` — iOS advertising, discovery, and data transfer via Multipeer Connectivity
  - `NearbyConnections.net.cs` — Generic .NET stub (throws `PlatformNotSupportedException`)
  - `NearbyConnections.log.cs` — Source-generated `ILogger` partial methods
  - `NearbyConnections.events.cs` — Event declarations and `internal On*()` raise helpers
- **Supporting types**:
  - `NearbyDeviceManager.cs` — Thread-safe device registry (`ConcurrentDictionary`-backed)
  - `PeerIdManager.ios.cs` — `MCPeerID` lifecycle management (iOS only)
  - `NearbyConnectionsOptions.cs` / `.android.cs` / `.ios.cs` — Immutable startup configuration
  - `OutgoingTransfer.cs` — Inactivity-timeout wrapper for outgoing file transfers

### Platform Dependencies

- **Android**: `Xamarin.GooglePlayServices.Nearby` package (`Directory.Packages.props`)
- **iOS**: Native `MultipeerConnectivity` framework

### Build Configuration

- **Platform Detection**: `IsTargetPlatformAndroid` and `IsTargetPlatformIos` properties (`Directory.Build.props`)
- **File Exclusion**: Platform-specific files are excluded from non-matching builds (`src/Directory.Build.targets`)
- **Global Usings**: Platform namespaces are auto-imported per target (`src/Directory.Build.targets`)

## Development Standards

- **Nullable Reference Types**: Enabled (`Directory.Build.props`)
- **Code Analysis**: Latest recommended level with warnings as errors (`Directory.Build.props`)
- **Documentation**: XML documentation required (`Directory.Build.props`)

## Current Implementation Status

Full P2P lifecycle is implemented on Android and iOS:
- Advertise / discover nearby devices
- Request, accept, and reject connections
- Send and receive bytes (`BytesPayload`) and files (`FilePayload`) with progress reporting
- Disconnect from peers

Generic .NET target throws `PlatformNotSupportedException` for all operations.

## iOS Configuration Requirements

Apps using this plugin need these `Info.plist` entries:

```xml
<key>NSBonjourServices</key>
<array>
  <string>_yourserviceid._tcp</string>
  <string>_yourserviceid._udp</string>
</array>
<key>NSLocalNetworkUsageDescription</key>
<string>Used to discover and connect to nearby devices.</string>
```

The service ID must match `NearbyConnectionsOptions.ServiceId` (default: app name).

## Project Strategy

This project follows an "Anti-Detail-Trap Strategy" focusing on shipping early and iterating fast (`.docs/PROJECTPLAN.md`). The 12-week plan prioritizes:
1. MVP Foundation (Weeks 1-4) - basic plugin + first NuGet publish
2. Core Features (Weeks 5-8) - production-ready functionality
3. Polish & Growth (Weeks 9-12) - community adoption

Priority is getting a working NuGet package published rather than building comprehensive features initially.

## Key Documentation References
- Coding Principles: @.claude/rules/coding-principles.md
- .NET 10 Overview: @.claude/rules/dotnet-10-overview.md
- MultipeerConnectivity API: @.claude/rules/multipeerconnectivity-api.md
- MAUI .NET 10 What's New: @.claude/rules/maui-dotnet-10.md
- MAUI Architecture Patterns: @.claude/rules/maui-architecture-patterns.md