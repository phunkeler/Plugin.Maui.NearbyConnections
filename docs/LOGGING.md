# Logging

This plugin logs through `Microsoft.Extensions.Logging`. It takes an `ILogger` from your app's DI
container and writes to whatever providers you have already configured — it does not install a
provider, write to a file, or send anything off the device.

If you configure nothing, you get the framework default: `Information` and above.

## What you see by default

Nothing, on a healthy session. Every routine event — devices appearing, connections forming,
payloads arriving — is at `Debug` or `Trace`, which the default threshold filters out.

At the default level you see only:

- **`Error`** — an operation is degraded and a consumer-visible thing failed.
- **`Warning`** — the plugin hit a problem and recovered, or dropped something it could not deliver.
- **`Information`** — one message: the iOS background teardown (see below).

That is the intended contract. If this plugin is chattering into your logs on a normal run, that is
a bug — please file it.

## The level contract

| Level | What it means | Volume |
|---|---|---|
| `Trace` | Per-payload: every message and transfer update crossing the wire. | Highest — one entry per payload |
| `Debug` | Per-device: discovery, handshakes, connection lifecycle. | One entry per device event |
| `Information` | A state change your app cannot observe any other way. | Rare |
| `Warning` | Recovered, or something was dropped. Worth reading, not necessarily acting on. | Rare |
| `Error` | An operation failed and something your app asked for did not happen. | Rare |

The one `Information` message is iOS backgrounding: MultipeerConnectivity does not survive
suspension, so the plugin tears the session down when the app backgrounds. Connections drop for a
reason that is not a defect and that your app cannot otherwise see — so it is on by default,
deliberately.

## Turning it up

Every message the plugin writes — session lifecycle, the platform layer, iOS peer bookkeeping,
iOS background teardown — shares one category: `Plugin.Maui.NearbyConnections.INearby`. One filter
covers the whole library; there is no finer-grained category to narrow into:

```csharp
builder.Logging.AddFilter("Plugin.Maui.NearbyConnections.INearby", LogLevel.Debug);
```

Pick your level by what you are chasing:

- **`Debug`** — devices not appearing, connections not forming, handshakes failing. This is the
  right level for almost all troubleshooting.
- **`Trace`** — payloads not arriving, or transfers stalling. Adds one entry per payload, so expect
  real volume on an active connection.

Distinguish where a message came from by its `EventId` range (below), not by category.

## Event IDs

Every message carries a stable `EventId`, so you can alert on a condition without matching on
message text. Ranges are allocated per owning type:

| Range | Owner |
|---|---|
| 1000–1099 | `Nearby` — session lifecycle |
| 2000–2099 | `PlatformNearby` — the platform layer |
| 3000–3009 | `PeerLookup` — iOS device ids and handle tracking |
| 3010–3099 | `AppLifecycleObserver` — iOS background teardown |

These IDs are worth wiring an alert to:

| ID | Level | Why it matters |
|---|---|---|
| `2027` | `Error` | A platform callback threw. Carries a `{Callback}` property naming which one — one filter covers all of them. |
| `2079` | `Warning` | A payload arrived but nothing was consuming the connection, so it is being buffered and lost. Almost always a wiring bug in the app; see [PAYLOAD-DELIVERY.md](PAYLOAD-DELIVERY.md). |
| `2084` / `2085` | `Error` | An advertising or discovery start failure could not be delivered to your code. You will see a normal end-of-stream instead of the error, so this log is the only record. |
| `2093` | `Warning` | Disposal stopped waiting for queued per-peer work, so the staging sweep that follows may delete a partly written file. The work is an inbound file copy on Android, or a rejected connection request on either platform. Expect it only when a transfer is wedged — a routine disposal drains in well under the timeout. |
| `2094` | `Warning` | Releasing one connection stopped waiting for that peer's queued work, so an inbound copy may fail when the payload handles are freed. Same expectation as `2093`. |
| `1013` | `Warning` | An expired inbound request could not be rejected. The device returns to `Visible` either way, but the platform may still hold the request open. |
| `1014` | `Error` | The expiry countdown for an inbound request failed. That request can stay outstanding until the session stops, so a stale row is the symptom. |
| `1023` | `Warning` | Stopping the session gave up waiting for its own background tasks (auto-accept, disconnect watchers). A straggler may still run, and its state writes can land after the registry clear. Routine stops join well under the bound. |
| `1024` | `Error` | A session-owned task failed without handling its own error. The session continues; the task's work did not finish. |

One ID you will see routinely rather than alert on: `1012` (`Debug`) records an inbound request
lapsing because the application did not answer it within
`NearbyOptions.InboundRequestTimeout`. That is an ordinary outcome, not a fault.

IDs are stable across releases and are never reused once shipped.

## Structured logging

Messages are written as templates with named properties, not interpolated strings, so a structured
sink captures the values as fields. `{DeviceId}`, `{Callback}`, and `{Writer}` are queryable rather
than baked into text — you can group every callback failure by callback name off a single event ID.

Exceptions are always passed as the exception argument, never as `ex.Message`, so your sink gets the
type and stack trace.

One identifier names a device everywhere: `{DeviceId}`. It carries the same value as
`NearbyDevice.Id`, on both platforms and at every level, so a single equality filter returns every
message about one device.

| Property | Type | Meaning |
|---|---|---|
| `DeviceId` | `string` | The remote device. Matches `NearbyDevice.Id`. Library-generated and session-scoped — see Privacy. |
| `DisplayName` | `string` | The remote device's user-chosen name. Identity data — see Privacy. |
| `Callback` | `string` | Which platform callback threw. Pairs with EventId `2027`. |
| `Writer` | `string` | Which internal stream dropped an event. |
| `PayloadId` | `long` | Platform-assigned payload handle. Android only. |
| `TimeoutSeconds` | `double` | The bound that elapsed, for the drain and timeout warnings. |

The plugin emits no logging scopes. Grouping is by `DeviceId`, which survives the thread hops that
`BeginScope` does not: platform callbacks are pumped onto thread-pool threads before consumers see
them, so a scope opened in a callback would not enclose the work it caused.

## Privacy

**Device display names and file paths appear in log messages**, at `Debug` for device names and at
`Error` for file paths. This is standard `ILogger` behaviour, not a defect — but display names are
user-chosen and often personal ("Sam's iPhone"), so treat them as identity data.

**A logged path for an inbound file carries a name the sending device supplied.** The library
reduces that name to its file-name component before use, so it cannot redirect a write, but the
name itself still reaches the sink as the remote peer wrote it. Treat it as untrusted, and as
identity data on the same footing as a display name.

**`DeviceId` is opaque and session-scoped, on both platforms.** It is generated by this library —
sixteen hexadecimal characters from a cryptographically secure random source — not derived from
anything either SDK supplied, and never from the display name. Google's endpoint id and Apple's peer
handle stay inside the library and never reach a log property.

It does not survive the session: the same physical device gets a different value on the next run, so
`DeviceId` cannot be used to recognise a device later or to line two sessions' logs up against each
other. Within one session it does identify a device, so treat it as a correlation handle rather than
as an anonymous statistic — but it carries none of the identity risk `DisplayName` does.

If those must not reach a sink — a remote/aggregated one especially — filter this library's
categories to `Warning` or above for that provider, or scrub the properties in the pipeline. Because
the values are structured properties rather than pre-formatted text, a sink-side redaction policy can
target them precisely.

## Turning it off

```csharp
builder.Logging.AddFilter("Plugin.Maui.NearbyConnections.INearby", LogLevel.None);
```

Logging must already be registered — `AddNearby` resolves `ILogger<INearby>` as a required service,
not an optional one. `MauiAppBuilder.CreateBuilder()` registers logging for you; code that builds a
bare `IServiceCollection` calls `AddLogging()` before `AddNearby`. Add no providers to that call and
every message becomes a no-op with no further configuration needed.

Logging is cheap when disabled. Every message is generated by
`[LoggerMessage]`, which checks `IsEnabled` before doing any formatting work — a filtered-out
`Trace` message on the payload hot path costs a single boolean check and allocates nothing.
