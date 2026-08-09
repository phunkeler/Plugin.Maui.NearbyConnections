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
- **Published identity is locked before 1.0** — `PackageId`, `AssemblyName`, `RootNamespace`, and
  the repo name stay `Plugin.Maui.NearbyConnections`. The NuGet package carries the full published
  history; changing any of them orphans it. All three are pinned explicitly in the csproj because
  `AssemblyName`/`RootNamespace` otherwise derive from the project filename — so a project rename
  would change them silently. Internal type names, file names, and folders are **not** locked and
  may be reorganised freely; see `DESIGN-PRINCIPLES.md`.

## Architecture

One public interface, registered as a DI singleton (one radio, one native session):

- **`INearby`** — the only public entry point, implemented by `NearbyImplementation`
  (`NearbyImplementation.{cs,state.cs,log.cs}`, at the project root alongside the
  interface). Owns device state, dispatcher marshalling, and the three lifecycle events.
- **`IPlatformNearby`** — internal. The raw platform streams, implemented by a single
  `sealed partial class` split across `Native/PlatformNearby.{shared,android,ios,net}.cs`.
  The `net10.0` target throws `PlatformNotSupportedException`, which is why `NearbyImplementation` depends
  on the interface rather than the concrete type — otherwise it is untestable off-device.

State vs. streams: device presence and connection lifecycle are **state** (`Devices` +
events); payloads are a **stream** (`NearbyConnection.ReceiveAsync`, one consumer per connection).

**All device-state mutation goes through `NearbyImplementation.state.cs` and is dispatcher-marshalled.**
Platform callbacks arrive on background threads; mutating an `ObservableCollection` or raising
`PropertyChanged` off the UI thread crashes XAML binding.

A pending handshake is a `TaskCompletionSource` in `_connectionTcs`. **Every failure path must
resolve or fault that TCS** or `AcceptAsync`/`ConnectAsync` hang forever.

Platform code lives in platform partials, never `#if` in shared logic.

### Folder layout

The four domain folders are the platform-neutral model; `Native/` is the layer that maps Google's
Nearby Connections and Apple's MultipeerConnectivity onto it. The tree is meant to state that
claim — if translation logic starts appearing outside `Native/`, the abstraction is leaking.

```
src/Plugin.Maui.NearbyConnections/
├── INearby.cs                     facade — at the root because it spans every domain
├── NearbyImplementation.{cs,state.cs,log.cs}
├── NearbyException.cs             root exception — at the root for the same reason
├── MauiAppBuilderExtensions.cs               registration entry points, beside the facade
├── ServiceCollectionExtensions.cs
├── Connections/   NearbyConnection, request, role, ControlMessage, EventArgs, connect timeout
├── Devices/       NearbyDevice, DeviceState, status, events, EndReason
├── Discovery/     availability + advertising/discovery failures
├── Payload/       NearbyPayload + NearbyBytesPayload/NearbyFilePayload — the data
├── Transfer/      progress, transfer timeout, outgoing transfer — the act of moving it
├── Options/       NearbyOptions + platform scopes + validators + the enums they use
├── Native/        IPlatformNearby, PlatformNearby.*, iOS peer identity,
│                  AppLifecycleObserver.ios
└── Platforms/     MAUI SDK convention folder (Android permissions) — NOT the same as Native/
```

**Nothing in `Native/` is public.** That is the quarantine, and it is checkable: a `public` type
declared there means the translation layer has leaked into the API surface.

The unit test project mirrors this layout, so a type's tests live in the folder matching its own.

**`Native/` vs `Platforms/`:** `Platforms/` is reserved by the MAUI SDK and carries its own
include/exclude rules. `Native/` is this plugin's translation layer. They are different things that
would read as the same thing if `Native/` were called `Platform/` — which is why it is not.

## Conventions

Mechanical style (braces, accessibility modifiers, `_camelCase` fields, `var` usage) is enforced by
`.editorconfig` + `TreatWarningsAsErrors` — the build fails on drift, so it is not restated here.
File-scoped namespaces are convention, not enforced.

- Nullable enabled — no `!` without a comment explaining why.
- `CancellationToken` on every public async method that does I/O.
- Logging via source-generated `ILogger` partial methods in `*.log.cs`, never string interpolation.
- Errors surface as typed exceptions (`NearbyException` and subclasses) at the public
  boundary. Never return `null` to signal failure.
- `ChannelWriter.TryComplete` returns `bool` — a `false` return means the fault was dropped and the
  consumer sees a normal end-of-stream. Log it.
- Scope broad catches with a filter, not a rethrowing catch:
  ```csharp
  catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
  ```
- Public members need XML docs (enforced). **Document platform divergence on the member itself.**
- Public types stay vendor-neutral: `Peer` is Apple's vocabulary, `Endpoint` is Google's. Internal
  code may use the platform's own term. **`DESIGN-PRINCIPLES.md` is authoritative on naming and
  structure** — read it before any naming or layout change; do not resolve its `OPEN` items silently.

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
