# IOptions Pattern Implementation

This plugin now supports Microsoft's IOptions pattern for configuration management.

## Usage Examples

### 1. Basic Registration with Delegate

```csharp
// In MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .AddNearbyConnections(options =>
            {
                options.AdvertiserOptions.ServiceName = "MyApp.Service";
                options.DiscovererOptions.ServiceName = "MyApp.Service";
                options.AutoAcceptConnections = true;
                options.DiscoveryTimeoutSeconds = 60;
                options.EventPublisher.EnableDeduplication = true;
                options.EventPublisher.DeduplicationWindowMs = 1000;
            });

        return builder.Build();
    }
}
```

### 2. Configuration from appsettings.json

```csharp
// In MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Add configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);

        builder
            .UseMauiApp<App>()
            .AddNearbyConnections(builder.Configuration);

        return builder.Build();
    }
}
```

### 3. Combined Configuration and Delegate

```csharp
// In MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.Configuration.AddJsonFile("appsettings.json", optional: true);

        builder
            .UseMauiApp<App>()
            .AddNearbyConnections(
                builder.Configuration,
                options =>
                {
                    // Override or add additional configuration
                    options.AutoAcceptConnections = false;
                });

        return builder.Build();
    }
}
```

## Configuration Structure

### appsettings.json

```json
{
  "NearbyConnections": {
    "AdvertiserOptions": {
      "ServiceName": "MyApp.NearbyService"
    },
    "DiscovererOptions": {
      "ServiceName": "MyApp.NearbyService"
    },
    "EventPublisher": {
      "EnableCorrelation": true,
      "EnableDeduplication": true,
      "DeduplicationWindowMs": 500,
      "MaxBufferSize": 1000
    },
    "AutoAcceptConnections": true,
    "DiscoveryTimeoutSeconds": 30,
    "AdvertisingTimeoutSeconds": 30
  }
}
```

## Validation

The plugin includes automatic validation of configuration options:

- `DiscoveryTimeoutSeconds`: Must be between 1 and 300 seconds
- `AdvertisingTimeoutSeconds`: Must be between 1 and 300 seconds
- `EventPublisher.DeduplicationWindowMs`: Must be between 100 and 10000ms
- `EventPublisher.MaxBufferSize`: Must be between 10 and 10000
- Service names cannot be null or whitespace

Invalid configurations will throw validation exceptions at startup.

## Using the Plugin

```csharp
// Inject the manager via DI
public class MyService
{
    readonly INearbyConnectionsManager _nearbyManager;

    public MyService(INearbyConnectionsManager nearbyManager)
    {
        _nearbyManager = nearbyManager;
    }

    public async Task StartNearbyConnections()
    {
        // Creates session using configured options
        using var session = await _nearbyManager.CreateSessionAsync();

        // Subscribe to events
        session.Events.Subscribe(evt =>
        {
            Console.WriteLine($"Event: {evt.GetType().Name}");
        });

        // Start discovery and advertising using configured options
        await session.StartDiscoveryAsync();
        await session.StartAdvertisingAsync();
    }
}
```

## Benefits

1. **Configuration Integration**: Works with .NET configuration system
2. **Validation**: Automatic validation with descriptive error messages
3. **Hot Reload**: Support for runtime configuration changes (when using IOptionsMonitor)
4. **Environment Support**: Easy configuration per environment
5. **Type Safety**: Strongly typed configuration with IntelliSense support
6. **DI Integration**: Natural dependency injection patterns