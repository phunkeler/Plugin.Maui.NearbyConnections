# Contributing

## Architecture

### One public interface, state and streams

Every session goes through two phases: **discovery/advertising** (learning who is nearby) and **connection** (exchanging data with a specific peer). Both live behind a single public interface, `INearbyConnections`.

The split that matters is not between phases but between *state* and *streams*:

| Shape | Used for | Why |
|---|---|---|
| Observable state — `Devices` + three C# events | Device presence and connection lifecycle | Presence *is* state, not a sequence of occurrences. A collection can be bound directly and read at any time; an event stream forces every consumer to rebuild the same collection from deltas. `NearbyDevice.Status` changes in place, so a bound row updates rather than moving between collections. |
| Stream — `NearbyConnection.ReceiveAsync` | Inbound payloads | Payloads are ordered, unbounded, and consumed once. The loop body is the seam where consumer async work goes, and it is awaited before the next payload is taken — a `void`-returning `EventHandler` cannot express that. See `docs/PAYLOAD-DELIVERY.md`. |

Internally `INearbyConnections` is implemented by `NearbyConnectionsImplementation`, which drives the internal `IPlatformNearbyConnections` (the raw platform streams) and projects its callbacks into device state. `IPlatformNearbyConnections` and its `AdvertiseAsync`/`DiscoverAsync` streams are implementation detail, not public API.

### Stream primitive — `System.Threading.Channels`

All async delivery is backed by `System.Threading.Channels`. Platform callbacks (Bluetooth/WiFi hardware events, arriving bytes) write into an unbounded channel; consumer `await foreach` reads from it. When there is nothing to read the read suspends cheaply until a write occurs. This is why the API is a stream and not polling.

`NearbyConnection` wraps its own per-connection `Channel<NearbyPayload>`, which the consumer enumerates directly.

### Two messaging patterns

| Pattern | Used for | Why |
|---|---|---|
| `Channel<NearbyPayload>` (single, `SingleReader = true`) | Per-connection payload stream (`NearbyConnection.ReceiveAsync`) | Single-consumer data pipe: each payload is consumed exactly once. Two concurrent `ReceiveAsync` enumerators on the same connection would race and steal items from each other — unbounded channels accept writes unconditionally so there is no back-pressure to expose the bug. The design enforces single-consumer by construction; fan out above the plugin. |
| `TaskCompletionSource` | Disconnect signal (`NearbyConnection.Disconnected`) | One-time completion event: `Task` natively multicasts to any number of awaiters at zero cost. |

### Threading — the session owns dispatcher marshalling

Platform callbacks arrive on SDK-owned background threads on both platforms. `NearbyConnectionsImplementation` funnels **every** `Devices` mutation, `NearbyDevice` property write, and event raise through `DispatchAsync`, so consumers observe all of them on the UI thread and bindings are safe without extra work. Nothing outside `NearbyConnectionsImplementation.state.cs` may touch device state.

This is why event handlers must be fast and must not do I/O: they run synchronously on the dispatcher. A throwing handler is caught and logged so it cannot take down the callback path, but it still starves the handlers after it.

### Subscription lifetime — the consumer's responsibility

The session is a singleton, so an event subscription without a matching `-=` keeps the subscriber alive for the life of the app, and re-subscribing (e.g. re-navigating to a page) fires handlers N times after N visits. A cancellation-scoped stream used to clean this up by ending its enumeration; C# events have no such affordance, so the discipline moved to the consumer.

`samples/NearbyChat` shows the required pattern: `BasePageViewModel.RegisterSessionSubscription(subscribe, unsubscribe)` detaches on navigate-away, and no page ViewModel subscribes directly. Payload loops need no equivalent — they self-terminate when the connection drops.

### DI registration

One call registers everything; there are no opt-in tiers.

**MAUI apps** — use `UseNearbyConnections()` on `MauiAppBuilder` (the MAUI-idiomatic style):

```csharp
builder.UseNearbyConnections(opts =>
{
#if IOS
    opts.InvitationTimeout = TimeSpan.FromSeconds(10);
#endif
});
```

**Testing seam** — `AddNearbyConnections()` also exists directly on `IServiceCollection`, and `INearbyConnections` is public, because consumers mock or implement the interface to test their own app code against the plugin. The public test-double constructor on `NearbyConnection` (`Connections/NearbyConnection.cs`) exists for exactly this: it lets a fake `INearbyConnections` hand real connection objects to the code under test.

```csharp
services.AddNearbyConnections();
```

Registered with `TryAddSingleton` — one radio, one native session, so the lifetime is platform-forced rather than a preference. Nothing auto-starts.

### Lifecycle wiring — app responsibility

The plugin does not attach to the host app's lifecycle. Stopping advertising and discovering when the app backgrounds is a product decision that belongs to the app, not the library. The platform (Android Doze, iOS background limits) terminates Nearby Connections / Multipeer sessions anyway, and the platform callbacks flow back through the plugin as disconnection events.

Apps that want to stop proactively — for example, to release Bluetooth/WiFi scan locks before the OS does — can wire lifecycle events themselves:

```csharp
// MauiProgram.cs
builder.ConfigureLifecycleEvents(lifecycle =>
{
#if ANDROID
    lifecycle.AddAndroid(android => android.OnStop(activity =>
    {
        var sp = IPlatformApplication.Current?.Services;
        _ = sp?.GetService<INearbyConnections>()?.StopAsync();
    }));
#elif IOS
    lifecycle.AddiOS(ios => ios.DidEnterBackground(app =>
    {
        var sp = IPlatformApplication.Current?.Services;
        _ = sp?.GetService<INearbyConnections>()?.StopAsync();
    }));
#endif
});
```

Use `OnStop` on Android and `DidEnterBackground` on iOS — these fire only on true backgrounding, not for transient interruptions such as notifications, dialogs, or incoming calls. `StopAsync()` leaves the session usable, so pages that start advertising or discovering on `NavigatedTo` resume naturally when the user returns.

### Platform implementations

Each platform implements the internal `IPlatformNearbyConnections` as a partial class sealed against `PlatformNearbyConnections`. Platform-specific files are excluded from non-matching build targets via `src/Directory.Build.targets`. Global usings per platform are also injected there.

## Day-to-day development

1. Work on a feature branch, not directly on `main`:
   ```bash
   git checkout -b feat/my-feature
   ```

2. Commit using the [Conventional Commits](#commit-messages) format — this is what drives the changelog and version bump.

3. Open a PR targeting `main`. The SonarCloud build must pass before merging.

4. Squash or merge into `main`. release-please will pick up the changes automatically.

## Releasing

Releases are fully automated once changes land on `main`. You do not manually edit versions anywhere.

### Normal release (patch or minor)

1. Commits accumulate on `main` via merged PRs.
2. [release-please](https://github.com/googleapis/release-please) automatically maintains an open **Release PR** titled `chore(main): release x.y.z`. It updates `version.txt` and maintains `CHANGELOG.md` (the file is created when the first Release PR merges) as new commits land.
3. When you're ready to ship, **merge the Release PR**.
4. Merging creates a git tag (e.g. `v0.2.0`) and a GitHub Release.
5. The tag triggers the `publish` workflow → approve the `nuget` environment deployment in GitHub Actions → package is pushed to NuGet.org.

### Pre-release (preview, rc)

release-please does not manage pre-release tags. The tag must always go on a `main` commit — MinVer derives the package version from the nearest tagged ancestor, so tagging a branch commit before merging produces the wrong version.

**Process:**

1. Open a PR from your feature branch targeting `main`
2. Squash merge the PR
3. Pull `main` locally and tag the squash commit:
   ```bash
   git checkout main && git pull
   git tag v0.3.0-preview.1
   git push origin v0.3.0-preview.1
   ```
4. `publish.yml` fires automatically → CI → NuGet → GitHub Release
5. Approve the deployment in the `nuget` environment when prompted

Or use the release script which validates the preconditions for you:

```bash
bash scripts/release.sh 0.3.0-preview.1
```

### Breaking changes (major bump)

Use a `!` suffix or a `BREAKING CHANGE:` footer in your commit message:

```
feat!: remove NearbyConnectionsEvents
```

release-please will propose a major version bump in the next Release PR.

## Commit messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/). Commit messages determine how the version is bumped and what appears in the changelog.

| Prefix | Effect | Example |
|---|---|---|
| `fix:` | Patch bump | `fix: handle null device in SetState` |
| `feat:` | Minor bump | `feat: add OutgoingTransferProgress event` |
| `feat!:` or `BREAKING CHANGE:` footer | Major bump | `feat!: rename SendAsync uri parameter` |
| `chore:`, `docs:`, `ci:`, `refactor:` | No bump | `docs: update iOS plist instructions` |

## Versioning

Versions are derived automatically from git tags at pack time via [MinVer](https://github.com/adamralph/minver). There is no version property in any project file — the git tag is the single source of truth.

## Running tests

### Unit tests

```bash
dotnet run --project test/Plugin.Maui.NearbyConnections.UnitTests
```

### UI tests

`test/NearbyChat.UiTests` is an Appium-driven xUnit suite that exercises the `samples/NearbyChat` sample app end-to-end (advertise/discover, connect, send/receive, disconnect) across 3 physical Android devices.

It can't be run standalone on a dev machine — it requires a live Appium server per device and expects these environment variables to already be set (normally supplied by CI):

```
DEVICE1_SERIAL, DEVICE2_SERIAL, DEVICE3_SERIAL   # adb device serials
APPIUM_1_URL, APPIUM_2_URL, APPIUM_3_URL          # Appium server URLs, one per device
APP_ACTIVITY                                      # resolved launcher activity (adb shell cmd package resolve-activity)
APP_PACKAGE                                       # optional, defaults to com.phunkeler.nearbychat
```

If `DEVICE1_SERIAL` is unset, every test in the suite skips rather than failing. In CI, the `UI Tests` workflow (`.github/workflows/ui-tests.yml`) builds the sample APK and dispatches the run to a private `android-lab` device farm that provisions the devices, installs the APK, grants permissions, and resolves the launcher activity before invoking these tests.

## Building

```bash
# All targets
dotnet build

# Specific platform
dotnet build -f net10.0-android
dotnet build -f net10.0-ios

# Pack
dotnet pack src/Plugin.Maui.NearbyConnections/Plugin.Maui.NearbyConnections.csproj -c Release
```

### Machine-local build overrides

`Directory.Build.props` holds project facts only — things true for every clone. Settings that are
true of *your machine* go in `Directory.Build.local.props` at the repo root. It is gitignored and
imported last, so its values win.

The usual reason to need one is a toolchain pin the workload manifest rejects. For example, an
Intel Mac cannot install Xcode 26.6, and the iOS workload manifest strictly rejects the 26.5 it is
stuck on:

```xml
<Project>
  <PropertyGroup>
    <ValidateXcodeVersion>false</ValidateXcodeVersion>
  </PropertyGroup>
</Project>
```

Never commit this kind of override to `Directory.Build.props` — it would silently disable the check
for every contributor and in CI.
