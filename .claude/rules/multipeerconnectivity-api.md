## MultipeerConnectivity Namespace (.NET iOS Bindings)

Source: https://learn.microsoft.com/en-us/dotnet/api/multipeerconnectivity?view=net-ios-26.2-10.0

Provides local (WiFi/Bluetooth) peer-to-peer messaging and data connections between iOS devices.

### Architecture: Two Phases

1. **Discovery Phase** - Associates `MCSession` objects between devices
   - **Advertisers**: Broadcast willingness to connect to a protocol
   - **Browsers**: Discover advertisers and invite them to sessions
2. **Session Phase** - `MCSession` is the channel for communication; lifecycle events handle connections, disconnections, transmissions, receptions

### Key Classes

| Class | Purpose |
|-------|---------|
| `MCPeerID` | Identifies a device in a multipeer connectivity network |
| `MCSession` | Persistent connection between multiple devices |
| `MCNearbyServiceAdvertiser` | Programmatic control for advertising the device |
| `MCNearbyServiceBrowser` | Programmatic browsing for advertising devices |
| `MCAdvertiserAssistant` | Convenience class managing advertising + user interaction |
| `MCBrowserViewController` | Standard UI for browsing peers |

### Key Delegates/Interfaces

| Interface | Purpose |
|-----------|---------|
| `IMCSessionDelegate` | Session lifecycle: connection changes, data reception |
| `IMCNearbyServiceAdvertiserDelegate` | Advertising and invitation events |
| `IMCNearbyServiceBrowserDelegate` | Peer-discovery events |
| `IMCAdvertiserAssistantDelegate` | Invitation presentation/dismissal |
| `IMCBrowserViewControllerDelegate` | Peer selection/cancellation UI events |

### Enums

| Enum | Values/Purpose |
|------|---------------|
| `MCEncryptionPreference` | Whether `MCSession` encrypts its connection |
| `MCSessionSendDataMode` | Reliable vs unreliable message delivery |
| `MCSessionState` | Connected, Connecting, NotConnected |
| `MCError` | Various multipeer connectivity errors |

### Programmatic Discovery Flow

**Advertiser side:**
1. Create `MCNearbyServiceAdvertiserDelegate`, assign to `MCNearbyServiceAdvertiser.Delegate`
2. Optionally create `MCSession` now or wait for invitation
3. Call `StartAdvertisingPeer()`
4. On `DidReceiveInvitationFromPeer`: invoke `invitationHandler(true, session)` to accept

**Browser side:**
1. Create `MCNearbyServiceBrowserDelegate`, assign to `MCNearbyServiceBrowser.Delegate`
2. Create `MCSession` (maintain single reference for all peers)
3. Call `StartBrowsingForPeers()`
4. On `FoundPeer`: call `InvitePeer()` with reference to `MCSession`

### Important Notes
- Advertiser and browser must use identical `serviceType` strings
- Each device should have a unique Peer ID
- Callbacks likely occur on background threads - use `InvokeOnMainThread` for UI updates
- MPC bridges through connected peers (WiFi-only device can reach Bluetooth-only via intermediary)
- MPC is NOT available for cross-platform scenarios (iOS-only)

### Delegates

| Delegate | Used In |
|----------|---------|
| `MCNearbyServiceAdvertiserInvitationHandler` | `DidReceiveInvitationFromPeer` callback |
| `MCSessionNearbyConnectionDataForPeerCompletionHandler` | `NearbyConnectionDataForPeer` completion |
