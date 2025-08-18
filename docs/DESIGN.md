# Design

## Follow [Plugin.Maui.Feature](https://github.com/jfversluis/Plugin.Maui.Feature)

## Architectural Decisions

### Advertiser/Discoverer Lifecycle Management

#### Design Decision: "Dispose and Recreate" Pattern

**Problem**: iOS `MCNearbyServiceAdvertiser` objects are immutable after creation. Once instantiated with specific `myPeerID`, `serviceType`, and `info` parameters, these cannot be modified. Android's `ConnectionsClient` is more flexible and allows parameter changes.

**Solution**: The `NearbyConnectionsImplementation` uses a "dispose and recreate" pattern where:

1. **On Stop**: The advertiser/discoverer is disposed and the reference is nulled
2. **On Start**: A new advertiser/discoverer instance is always created

**Benefits**:
- ✅ **Cross-platform consistency**: Both iOS and Android behave identically
- ✅ **Parameter flexibility**: Consumers can change display name, service type, and advertising info between sessions
- ✅ **Clean state**: Each advertising session starts with a fresh state
- ✅ **iOS requirement**: Only way to change `MCNearbyServiceAdvertiser` parameters

**Trade-offs**:
- ⚠️ **Performance**: Creates new objects for each start/stop cycle
- ⚠️ **Memory**: Increases GC pressure compared to object reuse
- ⚠️ **Event handlers**: Lost on disposal (by design for clean state)

**Implementation Note**: This pattern is applied consistently to both `IAdvertiser` and `IDiscoverer` implementations for symmetry.

**Supports Expanded Use Cases**
- Change name/service between advertising & discovery sessions
-  `IDictionary<string, string>`

### Consumer Usage Implications

Due to the "dispose and recreate" pattern, consumers should be aware of the following:

#### ✅ Supported Usage Patterns

<details>
<summary>Flexible advertising</summary>

```csharp
// ✅ Advertise with different parameters
await NearbyConnections.Current.StartAdvertisingAsync(new AdvertisingOptions
{
    DisplayName = "Device1",
    ServiceName = "game"
});

await NearbyConnections.Current.StopAdvertisingAsync();

// ✅ Different parameters - fully supported
await NearbyConnections.Current.StartAdvertisingAsync(new AdvertisingOptions
{
    DisplayName = "Device2",     // Changed
    ServiceName = "chat",        // Changed
    AdvertisingInfo = { ... }    // Changed
});
```

</details>

<details>
<summary>Event Consistency</summary>

```csharp
// ✅ Subscribe once - never need to resubscribe!
NearbyConnections.Current.AdvertisingStateChanged += OnAdvertisingStateChanged;

// Multiple start/stop cycles work seamlessly
await NearbyConnections.Current.StartAdvertisingAsync(options1);
await NearbyConnections.Current.StopAdvertisingAsync();

await NearbyConnections.Current.StartAdvertisingAsync(options2); // Different parameters
await NearbyConnections.Current.StopAdvertisingAsync();

// All events received despite underlying advertiser dispose/recreate
```

</details>

#### ⚠️ Considerations

- **State Reset**: Each advertising session starts completely fresh with no carryover state
- **Performance**: Brief overhead during start/stop cycles due to object creation


## Activities

### Discovery (iOS) & Pre-Connection (Android)

#### **Advertise**
During this activity apps make themselves known to others.

- To accomodate more advanced use cases, consider an "NearbyConnectionsAdvertiserManager" responsible for managing the creation/destruction of the INearbyConnectionsAdvertiser objects
    - On iOS, this will allow users more control over this activity ("discoveryInfo" in particular)

##### **Objects**:
- **MCNearbyServiceAdvertiser (iOS)**: Used to begin advertising
  - Initialization/Config:
    - MCPeerID - Represents the instance of your app running on the local device.
    - NSDictionary - Optional key-value pair data made available to discoverers upon discovery of this advertiser. Useful for providing richer information about the advertiser.
    - ServiceType - The Bonjour service type defined in the app's Info.plist

- **ConnectionsClient (Android)**: Used to begin advertising
  - Init/Config:
    - Needs Activity/Context
    -

#### **Discover**
During this activity apps search for nearby advertisers and initiate connection requests.

> [!NOTE]
> Apps can simultaneously act as both an advertiser and a discoverer.

#### **Objects**:
1. **MCNearbyServiceBrowser (iOS)**: Responsible for discovery
2. **MCNearbyServiceAdvertiser (iOS)**: Responsible for advertising
3. **ConnectionsClient (Android)**: Responsible for advertising & discovery

### Session (iOS) & Post-Connection (Android)