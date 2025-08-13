# Design

## Activities

### Discovery (iOS) & Pre-Connection (Android)

#### **Advertise**
During this activity apps make themselves known to others.

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