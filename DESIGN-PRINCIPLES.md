# Design principles

**Why the naming and structure contract is what it is.** The binding rules themselves live in
`.claude/rules/naming.md`, which Claude Code loads automatically whenever it touches
`src/Plugin.Maui.NearbyConnections/`. This document is the reasoning behind them — read it when you
want to know *why* a rule exists, or when you are about to argue with one.

Where this document and **verified repo evidence** disagree, this document is the bug. Fix it here,
in a commit, rather than working around it.

| I want to… | Read |
|---|---|
| Know the rule | `.claude/rules/naming.md` |
| Know why the rule exists | This document |
| Know what is still undecided | [Open questions](#open-questions), then `docs/ARCHITECTURE.md` §5 |
| Know the architecture | `docs/ARCHITECTURE.md`, then `AGENTS.md` for the as-is shape |
| Know a platform gotcha | `AGENTS.md` → *The things people get wrong*, and `docs/DEVICE-LIFECYCLE.md` |
| Know the outstanding work list | `docs/ARCHITECTURE.md` §5, the migration map (GitHub issues were bulk-closed and left closed) |

---

## Published identity is locked; everything else is free

The gate covers **published identity only**:

| Surface | Value | Status |
|---|---|---|
| `PackageId` | `Plugin.Maui.NearbyConnections` | 🔒 locked through 1.0 |
| `AssemblyName` | `Plugin.Maui.NearbyConnections` | 🔒 locked through 1.0 |
| `RootNamespace` / public namespace | `Plugin.Maui.NearbyConnections` | 🔒 locked through 1.0 |
| Repository | `phunkeler/Plugin.Maui.NearbyConnections` | 🔒 locked through 1.0 |
| Type names, file names, folders | — | free, reorganise at will |

**Why locked:** the NuGet package carries the full published history (`0.0.0-alpha` →
`0.3.0-preview.1`), and the `v0.3.0-preview.1` tag shipped a real public API to real consumers.
Changing any locked row orphans that package — the old ID keeps the download history and the new one
starts at zero, with no automatic path between them.

**Why the rest is free:** none of it is consumer-visible. The quarantine that matters is the public
surface, and `PublicAPI.Unshipped.txt` already enforces that mechanically.

**`AssemblyName` and `RootNamespace` are pinned explicitly in the csproj.** They otherwise derive
from the project filename, so renaming the project file would silently change the assembly and every
public namespace — consumer-visible breakage with no diff in the file that caused it. Pinning them
is what makes the rest of the tree safe to reorganise.

The phrase "Nearby Connections" survives **only** where it names Google's actual technology:

```
GOOD  Android uses Google Nearby Connections.
BAD   Nearby Connections is the abstraction exposed by this library.
```

---

## Why the vocabulary is vendor-neutral

`Peer` is Apple's word. `Endpoint` is Google's. `Strategy`, `Browser`, and `Advertiser` are lifted
straight from one SDK or the other. A public API that borrows either vendor's vocabulary teaches the
consumer the wrong mental model — it implies the abstraction is a thin wrapper over that one
platform, and it ages badly the moment the platform does.

That last risk is not hypothetical: Apple deprecated MultipeerConnectivity, and the iOS
implementation will migrate to Network.framework (issue #45, closed with the rest of the tracker; the
work is deferred to post-1.0). Every MPC term on the public surface would have become a lie.
Vendor-neutral names survive that migration unchanged.

**`Session` gets its own ban** because Apple's `MCSession` makes it tempting. The consumer-facing
object is a *capability* — one radio, one DI singleton — not a session the consumer opens and
closes. Exposing "session" invites lifecycle assumptions the platform cannot honour.

## Why collision resistance beats brevity

A MAUI app already has `Device`, `Application`, `Connectivity`, and `Permissions` in scope. A bare
`Device` or `Connection` on this surface forces consumers into alias directives or fully-qualified
names in their own code.

The payload pair proves the point: `BytesPayload` and `FilePayload` are exactly the names an app
that models its own payloads would define itself. They are now `Nearby`-prefixed.

The exemption is principled, not lazy: `ConnectionRole` is a compound domain noun with no plausible
MAUI collision. (`DeviceState` was a second; it was deleted when the connection moved off the
device. `EndReason` was a third; it became internal once it was clear no consumer could observe a
value — a handshake failure surfaces as the thrown exception, and a drop as the device returning to
`Visible`.)

## Why payload and transfer are separate concepts

A payload is the data; a transfer is the act of moving it. Collapsing them produces an API where you
cannot describe a payload that failed to transfer, or a transfer carrying a payload you have not yet
received.

The folder layout used to say the opposite of this rule — all three payload types lived in
`Transfer/`. They now live in `Payload/`, so the tree states the claim the rules make.

## Why the platform boundary is a folder, not a convention

`Native/` is checkable. "Nothing in `Native/` is public" can be verified by grep, and a leak shows up
as an RS0016 against the PublicAPI baseline. A convention that lives only in prose gets violated
silently; one that the build can see does not.

`Platforms/` is the MAUI SDK's reserved folder with its own include/exclude rules. Naming this
plugin's translation layer `Platform/` would make two genuinely different things read as the same
thing — which is precisely why it is `Native/`.

## Why platform-divergent config is named, not hidden

A setter that silently does nothing on the current platform is a defect: the consumer writes code
that looks correct, compiles, and has no effect. Naming the platform at the call site
(`options.Android.Topology`) makes the divergence impossible to miss.

The machine-checkable form is that **all three PublicAPI baselines stay identical**. Before this
rule, `Topology` existed only on `net10.0-android` and `EncryptionPreference` only on `net10.0-ios`,
so shared code could not set them without `#if`.

### The escape-hatch policy (decision D4)

**Named platform scopes or nothing, opened on the first concrete request.** A one-platform
capability with no concrete requester stays off the surface. When a real consumer asks for one, it
opens as a named scope (`options.Android.X`, `options.Apple.X`), never as a silent shared member. A
raw-handle hatch — any member that exposes an SDK object such as an `MCPeerID` — stays refused: it
breaks the `Native/` quarantine, and it would break every consumer at the MultipeerConnectivity
exit. `docs/ARCHITECTURE.md` §1 lists the refused stories and the capabilities that wait for a
requester.

## Why `NearbyDevice` is an immutable record with a flat `Status`

`NearbyDevice` is a `sealed record` carrying a flat `NearbyDeviceStatus` and a nullable
`ConnectionRole`. It is a snapshot: a transition publishes a new instance rather than mutating the
old one, and consumers observe that as an `Updated` entry on `Devices.Changes`.

Two earlier designs were tried and removed, and the reasoning is kept because both are tempting to
propose again:

- **A sealed `DeviceState` hierarchy carrying the connection**, with `Status` projected from it. It
  forced every XAML binding through a converter, and because C# has no exhaustiveness checking for
  sealed hierarchies, a missing arm in the projection would report a wrong lifecycle position
  forever with no compile error. The connection moved to a keyed lookup on the session instead —
  see [the rules file](.claude/rules/naming.md) — and the hierarchy was deleted.
- **A mutable device raising `PropertyChanged`.** It made device state shared mutable state readable
  from any thread, and a silent write froze bound UI with no compile error and no test failure. The
  record removes the failure mode rather than documenting it: there is nothing to write silently.

The cost is that binding needs a collection that translates the delta stream into
`INotifyCollectionChanged`. That is `NearbyDeviceCollection<TRow>`, which is deliberate — it confines UI
thread knowledge to exactly one type instead of spreading dispatcher affinity across the model.

## Why some odd-reading names stay

A name that reads oddly is not automatically wrong — check what it maps to first.

`NearbyConnectionType` (`Balanced`/`HighBandwidth`/`NonDisruptive`) reads like a performance
preference rather than a "type", but it maps to Google's real `SetConnectionType()`, a genuinely
distinct knob from `Strategy`. The neutral enum exists precisely so consumers never reference
`Android.Gms.Nearby.Connection.Strategy`. Renaming it for readability would break the mapping the
name is documenting.

---

## Open questions

Deliberately undecided. **Raise them; do not resolve them silently.**

| Question | Status |
|---|---|
| Final package name, and whether a new repo/package is created | Gated by the rename guard (issue #52, closed with the tracker) |
| iOS `StartFailureGraceWindow` (`Native/PlatformNearby.ios.cs`) | Open. MultipeerConnectivity has no start-success callback, only a delegate that fires on failure — so a fixed 250ms window is used to decide whether `StartAdvertisingAsync`/`StartDiscoveryAsync` should fault or return successfully. A device slow enough to blow past the window gets a false "started successfully," with the real failure only surfacing later as a stream fault plus a log line. No better signal exists in the MPC API to key off instead; raised here rather than resolved silently. |

### Resolved, kept for the reasoning

**`InvitationTimeout` → `ConnectTimeout`.** Split, not just renamed. "Invitation" was MPC vocabulary, banned from the public contract. The one option became three, each named for what it bounds: `ConnectTimeout` for `ConnectAsync`, `AcceptTimeout` for `AcceptAsync`, and `InboundRequestTimeout` for a request nobody answered. **Recollapsed to one (2026-08-26):** the split existed because neither side could observe the other's deadline, so each side guessed. Once the initiator's `ConnectTimeout` is declared on the wire with the request itself, the other two options were independent guesses at a value the advertiser now simply knows — the request row expires at the declared deadline, and the accept's bound is that deadline's remaining window. `ConnectTimeout` kept its name over `HandshakeTimeout`: the handshake is only the last slice of the window it bounds, and the name stays anchored to the operation the setter serves.

**Remote-declared durations, locally clamped.** The one-deadline model makes a remote peer's number program local durations (request-row lifetime, accept await). The pattern that makes this safe is the gRPC deadline-propagation shape: the client declares, the server clamps and honors. The clamp — `OfferWindow.Max`, an internal five-minute constant — is the single security parameter: every exposure the declared value creates scales linearly with it, so any future tightening is one constant. Durations, not timestamps, travel on the wire (the devices share no clock), degenerate declarations hurt only their sender, and the unconsented auto-accept path honors a declaration only up to the assumed 30-second window.

Everything previously listed here has been resolved and moved into the rules file: the primary
interface is `INearby`, the device noun is `NearbyDevice`, payload types are `Nearby`-prefixed and
live in `Payload/`, and the exception base is `NearbyException` with five `sealed` prefixed
subclasses each filed under the folder its domain owns.

---

## Verifying the repo against this document

These checks are reproducible, which is why no dated audit snapshot is kept here — a snapshot goes
stale the moment code changes, and a stale audit is worse than none.

```bash
# No vendor types on the public surface (expect no output)
grep -rE "Android\.Gms|MultipeerConnectivity|MCSession|MCPeerID" \
  src/Plugin.Maui.NearbyConnections/PublicAPI/

# No banned vocabulary as public type names (expect no output)
grep -rE "^Plugin\.Maui\.NearbyConnections\.(Nearby)?(Session|Peer|Endpoint|Browser|Advertiser|Strategy|Radio)$" \
  src/Plugin.Maui.NearbyConnections/PublicAPI/

# No public TYPES in Native/ (expect no output). Public *members* of internal types are
# fine and expected — the rule is about the type's own accessibility.
grep -rnE "^\s*public\s+(sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+|record\s+)*(class|interface|struct|enum|record|delegate)\b" \
  src/Plugin.Maui.NearbyConnections/Native/*.cs

# No Nearby-prefixed methods (expect no output)
grep -rE "\.Nearby[A-Za-z]+Async\(" src/Plugin.Maui.NearbyConnections/

# All three baselines identical (expect no output)
diff src/Plugin.Maui.NearbyConnections/PublicAPI/net10.0/PublicAPI.Unshipped.txt \
     src/Plugin.Maui.NearbyConnections/PublicAPI/net10.0-android/PublicAPI.Unshipped.txt
```

The last one expects no output at all: the three baselines are currently byte-identical. (An earlier
revision of this document noted a `Plugin.Maui.NearbyConnections.Resource` line in the Android
baseline, generated by the Android SDK's resource designer. It is no longer present — treat any
reappearance as a real diff to explain, not a known exception to wave through.)

Full build and test commands are in `AGENTS.md` → Commands.

---

## Provenance

Derived from a working specification drafted with ChatGPT (2026-08-09), vetted against the repo
before adoption. Recorded because it explains why the document is trustworthy: every claim was
checked against code, and four were wrong.

- **Rename premise** — the source assumed a clean redesign with no shipped history, omitting the
  published package, the two prior renames, and the finality guard. Its proposed eleven-commit
  staged sequence is exactly the shape that guard rejects.
- **`ConnectionType`** — the source proposed renaming it to `ConnectionPreference`/`ConnectionMode`.
  Contradicted by the Android implementation; it maps to a real, distinct native knob.
- **`OutgoingTransfer`** — the source asked whether it belongs on the public API. It is already
  `internal`. Question dropped.
- **`Session`** — the source warned against reintroducing it. It was never there.
- **Options TFM asymmetry** — the source was silent; the defect was visible only by diffing the
  three PublicAPI baselines.
