# NearbyChat UI Tests — Appium

End-to-end tests for the NearbyChat sample app. Drives two physical Android devices via
Appium/UIAutomator2 to verify real P2P Nearby Connections behavior.

## Prerequisites

- [Appium 2.x](https://appium.io/docs/en/latest/) and the UIAutomator2 driver:
  ```sh
  npm install -g appium
  appium driver install uiautomator2
  ```
- Two physical Android devices with USB debugging enabled
- NearbyChat deployed to both devices via `dotnet build -f net10.0-android` + VS deploy

> **Device prep is automatic.** `[AssemblyInitialize]` uses the `AndroidSdk` NuGet package to
> wake screens, keep them on, and dismiss the lock screen before the session starts.
> ADB does not need to be in PATH on the local machine — `AndroidSdk` downloads platform-tools
> if needed. On the Pi self-hosted runner it is already present.

## Setup

**1. Start the Appium server** (leave it running in a terminal):
```sh
appium
```

**2. Connect both devices and find their serials:**
```powershell
.\Setup\setup-android.ps1
```

**3. Edit `NearbyConnections.runsettings`** with the printed serials and Appium URL:
```xml
<Parameter name="DEVICE1_SERIAL" value="ABC123" />
<Parameter name="DEVICE2_SERIAL" value="DEF456" />
<Parameter name="APPIUM_SERVER_URL" value="http://192.168.x.pi:4723" />
```

## Running Tests

This project uses `MSTest.Sdk` (Microsoft Testing Platform). Use `dotnet run`:

```sh
dotnet run --project test/Plugin.Maui.NearbyConnections.UiTests -- --settings NearbyConnections.runsettings
```

Filter to a single test class:
```sh
dotnet run --project test/Plugin.Maui.NearbyConnections.UiTests -- --settings NearbyConnections.runsettings --filter "ClassName=ConnectionLifecycleTests"
```

## Evidence

Screenshots are saved to `evidence/` and attached to the MSTest result automatically.

## Device Roles

- **Device 1** (`DEVICE1_SERIAL`) = Advertiser
- **Device 2** (`DEVICE2_SERIAL`) = Discoverer

## Future: DevFlow

`Microsoft.Maui.DevFlow.Agent` is already registered in NearbyChat (`MauiProgram.cs`) and
`Microsoft.Maui.DevFlow.Driver` is referenced in this project. See `DevFlow/DevFlowAgent.cs`
for the intended migration path when DevFlow stabilises out of preview.

## Troubleshooting

| Symptom | Check |
|---------|-------|
| `Required environment variable 'DEVICE1_SERIAL' is not set` | Update `NearbyConnections.runsettings` or set `$env:DEVICE1_SERIAL` |
| `AndroidDriver` constructor times out | Appium server not running, or device serial wrong |
| `WaitForText("AdvertisingStatus")` times out | `IsVisible="False"` on the status label — must be `Opacity="0"` |
| `WaitForElementsByPrefix("Connect_")` times out | Devices too far apart, BT/WiFi off, or advertising didn't start |
