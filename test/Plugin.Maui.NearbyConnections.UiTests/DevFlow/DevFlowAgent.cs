// -------------------------------------------------------------------------------------------------
// DevFlow — future replacement for AppiumAgent
// -------------------------------------------------------------------------------------------------
// NearbyChat already registers the DevFlow in-app agent (AddMauiDevFlowAgent() in MauiProgram.cs).
// Microsoft.Maui.DevFlow.Driver is referenced in this project and ready to use once the API
// stabilises out of preview.
//
// When ready, replace AppiumAgent with a DevFlowAgent that connects to the in-app HTTP endpoint:
//
//   var driver = await MauiDriver.ConnectAsync(deviceEndpoint);
//   await driver.FindElement(By.AutomationId("Advertise")).TapAsync();
//   var tree   = await driver.GetVisualTreeAsync();
//
// Benefits over Appium:
//   - No Appium server, no Docker, no UiAutomator2 bootstrap
//   - Direct HTTP to in-app agent — faster session startup
//   - MAUI visual tree awareness (not generic Android view hierarchy)
//   - MCP server integration for AI-driven test authoring
//   - Cross-platform: same driver API for iOS, Mac Catalyst, and Windows
//
// Device prep (wake/unlock) stays with DevicePrep.cs regardless of which agent is active.
//
// DevFlow NuGet packages (all preview, may change):
//   In-app agent : Microsoft.Maui.DevFlow.Agent         (NearbyChat.csproj)
//   Test driver  : Microsoft.Maui.DevFlow.Driver         (this project)
//   Source       : https://github.com/dotnet/maui-labs
// -------------------------------------------------------------------------------------------------
