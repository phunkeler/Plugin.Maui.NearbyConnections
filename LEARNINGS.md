# LEARNINGS.md

Platform quirks and toolchain traps that have already cost debugging time here. Each entry exists
because something behaved differently than the documentation or intuition suggested.

Add to this file whenever a non-obvious cause is found. A quirk recorded once is a quirk nobody
re-derives.

## iOS — MultipeerConnectivity

**`MCSessionState.Connecting` is not guaranteed to occur.** A peer can transition straight to
`NotConnected`. Treating `Connecting` as a required waypoint is a latent hang — the state machine
must accept the direct transition.

**`NotConnected` carries no reason.** Rejected, timed out, and walked-out-of-range are
indistinguishable at the API level. The rejecting peer knows why, so the workaround is to send a
control message before tearing down. Apple's own DTS guidance endorses this approach.

**`ConnectedPeers` lags.** The collection is not updated synchronously with the state callback, so
reading it immediately inside a delegate can return a stale membership set.

## Android — Nearby Connections (GMS)

**There is no native connect timeout.** The platform will wait indefinitely; the timeout has to be
imposed by the plugin.

**Unclean timeouts poison the endpoint.** Abandoning a connection attempt without properly
cancelling it leaves the GMS endpoint in a state where subsequent attempts to the same endpoint
fail. Cancel explicitly on every timeout path.

## .NET / MAUI toolchain

**`dotnet test` does not work in this repo.** VSTest is unsupported on the .NET 10 SDK. Use
`dotnet run --project test/Plugin.Maui.NearbyConnections.UnitTests/Plugin.Maui.NearbyConnections.UnitTests.csproj`.

**MAUI 10.0.41 clears `content-desc`.** UI tests must locate elements by resource-id
(`MobileBy.Id`), never `AccessibilityId`. An `AutomationId` maps to resource-id, not to
`content-desc`.

## Library internals

**`ChannelWriter.TryComplete` returns `bool`, and a `false` return is silent data loss.** It means
the fault was dropped and the consumer sees a normal end-of-stream instead of an error. Always log
a `false` return.

**Payload consumers must be constructed before the connection is established.** `Devices.Changes`
has no replay, so a lazily-constructed DI singleton consumer misses the transition and payloads
silently vanish. Force construction eagerly. (Was `ConnectionEstablished` before that event was
removed; the hazard is unchanged.)

**An `async` iterator does not run a single line until the first `MoveNextAsync`.** Anything a
`GetAsyncEnumerator` implementation does before its first `yield` — registering a subscription, in
particular — is deferred with it. A consumer that calls `GetAsyncEnumerator`, reads current state,
and only then starts iterating loses every event published in that window, with no error anywhere.
Subscribe in a plain (non-iterator) method that returns the iterator, as
`NearbyDeviceRegistry.ChangeStream` does. Note that a test helper which calls `MoveNextAsync` before
handing the enumerator over will hide this completely.
