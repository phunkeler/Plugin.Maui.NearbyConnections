# AGENTS.md

Guidance for AI agents (GitHub Copilot, Claude Code, MCP-based assistants) and new contributors.

A .NET MAUI plugin for peer-to-peer connectivity with nearby devices, unifying Google's Nearby
Connections (Android) and Apple's MultipeerConnectivity (iOS). Shipped on NuGet as
`Plugin.Maui.NearbyConnections`.

## Commands

```bash
# All three TFMs must build warning-free — TreatWarningsAsErrors is on
dotnet build src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -f net10.0
dotnet build src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -f net10.0-android
dotnet build src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -f net10.0-ios

# Unit tests — `dotnet run`, NOT `dotnet test` (VSTest is unsupported on the .NET 10 SDK)
dotnet run --project test/Plugin.Maui.NearbyConnections.UnitTests/Plugin.Maui.NearbyConnections.UnitTests.csproj
```

## The things people get wrong

- **`dotnet test` does not work here.** Use `dotnet run --project`.
- **The public API surface is build-enforced.** Anything newly `public` fails the build until
  recorded in `src/Plugin.Maui.NearbyConnections/PublicAPI/{tfm}/PublicAPI.Unshipped.txt`. Build,
  read the RS0016 errors, add the listed lines. Never suppress the analyzer to go green.
- **The dominant defect class is a fix applied to one platform partial and not its sibling.** When
  changing `*.android.cs`, grep `*.ios.cs` for the same shape, and vice versa.
- **`MCSessionState.Connecting` is not guaranteed to occur on iOS** — a peer can go straight to
  `NotConnected`. Treating it as a required waypoint is a latent hang.
- **Every `catch` on a callback or error path must log.** Silent catches have already cost real
  debugging time here.
- **Plugin identity is locked before 1.0** — no package, namespace, assembly, or repo renames.
  The NuGet package carries the full published history; a rename would orphan it.

## Architecture

One public interface, registered as a DI singleton (one radio, one native session):

- **`INearbyConnections`** — the only public entry point, implemented by `NearbyConnectionsImplementation`
  (`Session/NearbyConnectionsImplementation.{cs,state.cs,log.cs}`). Owns device state, dispatcher marshalling, and the
  three lifecycle events.
- **`IPlatformNearbyConnections`** — internal. The raw platform streams, implemented by a single
  `sealed partial class` split across `Connections/NearbyConnections.{shared,android,ios,net}.cs`.
  The `net10.0` target throws `PlatformNotSupportedException`, which is why `NearbyConnectionsImplementation` depends
  on the interface rather than the concrete type — otherwise it is untestable off-device.

State vs. streams: device presence and connection lifecycle are **state** (`Devices` +
events); payloads are a **stream** (`NearbyConnection.ReceiveAsync`, one consumer per connection).

**All device-state mutation goes through `NearbyConnectionsImplementation.state.cs` and is dispatcher-marshalled.**
Platform callbacks arrive on background threads; mutating an `ObservableCollection` or raising
`PropertyChanged` off the UI thread crashes XAML binding.

A pending handshake is a `TaskCompletionSource` in `_connectionTcs`. **Every failure path must
resolve or fault that TCS** or `AcceptAsync`/`ConnectAsync` hang forever.

Platform code lives in platform partials, never `#if` in shared logic.

## Conventions

- Braces on every `if`/`else`/`for`/`foreach`/`while`/`do`, including single-line bodies.
- Omit redundant accessibility modifiers. File-scoped namespaces. Private fields `_camelCase`.
- Nullable enabled — no `!` without a comment explaining why.
- `CancellationToken` on every public async method that does I/O.
- Logging via source-generated `ILogger` partial methods in `*.log.cs`, never string interpolation.
- Errors surface as typed exceptions (`NearbyConnectionsException` and subclasses) at the public
  boundary. Never return `null` to signal failure.
- `ChannelWriter.TryComplete` returns `bool` — a `false` return means the fault was dropped and the
  consumer sees a normal end-of-stream. Log it.
- Scope broad catches with a filter, not a rethrowing catch:
  ```csharp
  catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
  ```
- Public members need XML docs (enforced). **Document platform divergence on the member itself.**
- Public types stay vendor-neutral: `Peer` is Apple's vocabulary, `Endpoint` is Google's. Internal
  code may use the platform's own term.

## Tests

MSTest v4 with strict Arrange / Act / Assert — blank line between sections, all three comments
present even when trivial. Compute expected values in Arrange; no logic in Assert.

MSTest specifics: `Assert.ThrowsExactly<T>` (not `ThrowsException<T>`), `Assert.IsEmpty(x)`,
`Assert.HasCount(n, x)` (count first). Class names cannot end in `Collection`. Parallelization is
method-level, so tests must not share mutable state.

**UI tests** (`test/NearbyChat.UiTests/`, Appium) locate elements by resource-id (`MobileBy.Id`),
not `AccessibilityId` — MAUI clears `content-desc`. Never change an `x:Name` or `AutomationId`
without updating that suite. It is historically flaky for environment reasons; triage a red run
against known flakes before blaming a code change.

Platform partials have **no automated unit coverage** — they need real SDKs. The UI suite is the
only automated check on that code.

## Further reading

- `docs/DEVICE-LIFECYCLE.md` — device lifecycle states and platform capability gaps
- `docs/PAYLOAD-DELIVERY.md` — why payloads are delivered as events, not an async stream
- `CONTRIBUTING.md` — build, release, and contribution process
