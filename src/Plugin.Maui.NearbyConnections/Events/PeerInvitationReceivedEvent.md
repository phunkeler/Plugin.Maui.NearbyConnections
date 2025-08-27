# PeerInvitationReceivedEvent Native API Mapping

## Overview

The `PeerInvitationReceivedEvent` provides a cross-platform abstraction for peer invitation events that occur when another device initiates a connection request.

## Native API Mappings

### Android - Google Nearby Connections

**Native Callback**: `IConnectionLifecycleCallback.OnConnectionInitiated`

```java
@Override
public void onConnectionInitiated(String endpointId, ConnectionInfo connectionInfo) {
    // Maps to PeerInvitationReceivedEvent
}
```

**Property Mappings**:
- `ConnectionEndpoint` ← `endpointId`
- `InvitingPeer.Id` ← `connectionInfo.getEndpointName()`
- `InvitingPeer.DisplayName` ← `connectionInfo.getEndpointName()`
- `AuthenticationToken` ← `connectionInfo.getAuthenticationDigits()`
- `InvitationContext` ← Custom context data (if any)
- `RequiresAcceptance` ← Always `true` (manual connection flow)

**Usage Example**:
```csharp
// In Android platform implementation
var invitationEvent = new PeerInvitationReceivedEvent
{
    InvitingPeer = new PeerDevice
    {
        Id = connectionInfo.EndpointName,
        DisplayName = connectionInfo.EndpointName,
        ConnectionState = PeerConnectionState.Connecting
    },
    AuthenticationToken = connectionInfo.AuthenticationDigits,
    ConnectionEndpoint = endpointId,
    RequiresAcceptance = true
};
```

### iOS - MultipeerConnectivity

**Native Delegate**: `MCNearbyServiceAdvertiserDelegate.DidReceiveInvitationFromPeer`

```objc
- (void)advertiser:(MCNearbyServiceAdvertiser *)advertiser 
didReceiveInvitationFromPeer:(MCPeerID *)peerID 
       withContext:(NSData *)context 
 invitationHandler:(void (^)(BOOL accept, MCSession *session))invitationHandler
```

**Property Mappings**:
- `ConnectionEndpoint` ← `peerID.DisplayName`
- `InvitingPeer.Id` ← `peerID.DisplayName`
- `InvitingPeer.DisplayName` ← `peerID.DisplayName`
- `AuthenticationToken` ← `null` (iOS doesn't provide authentication tokens)
- `InvitationContext` ← `context` data (converted to string)
- `RequiresAcceptance` ← Always `true` (manual invitation flow)

**Usage Example**:
```csharp
// In iOS platform implementation
var invitationEvent = new PeerInvitationReceivedEvent
{
    InvitingPeer = new PeerDevice
    {
        Id = peerID.DisplayName,
        DisplayName = peerID.DisplayName,
        ConnectionState = PeerConnectionState.Connecting
    },
    AuthenticationToken = null,
    InvitationContext = context?.ToString(),
    ConnectionEndpoint = peerID.DisplayName,
    RequiresAcceptance = true
};
```

## Event Processing Flow

1. **Native Callback Triggered**: Platform-specific connection initiation occurs
2. **Event Creation**: Platform implementation creates `PeerInvitationReceivedEvent`
3. **Event Processing**: `IPeerInvitationReceivedEventProcessor.ProcessInvitationAsync()` is called
4. **Response Handling**: Processor calls either `AcceptInvitationAsync()` or `RejectInvitationAsync()`
5. **Platform Response**: Native APIs are called to complete the invitation flow

## Security Considerations

- **Android**: Uses `AuthenticationDigits` for manual verification
- **iOS**: Relies on service type filtering and user acceptance
- Both platforms support context data for additional verification

## Implementation Notes

- The event normalizes the different invitation flows across platforms
- `RequiresAcceptance` is currently always `true` but could be `false` for auto-accept scenarios
- Platform-specific details are abstracted through the common event interface
- Event processors should handle platform differences in authentication and context handling