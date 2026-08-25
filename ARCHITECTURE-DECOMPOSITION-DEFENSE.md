# Architecture decomposition defense — `docs/ARCHITECTURE.md` §4

Date: 2026-08-25. Scope: an adversarial pass over §4 (*Internal decomposition — Proposed*,
`docs/ARCHITECTURE.md:299-677`). Sections 1–3 are approved and serve as acceptance criteria.
This document is the audit trail. The amended shape lands in `INTERNAL-DECOMPOSITION.md`.

Method: eleven elements, each attacked, defended, and given a verdict — **holds**, **amend**,
or **falls**. Three calibration probes ran first and set the depth. The synthesis re-runs every
contract, story, and fact trace against the amended shape.

## Confidence labels, same discipline as the prior reviews

- **confirmed** — the cited code or document proves the claim by inspection.
- **needs-failing-test** — the mechanism is visible, but no test reproduced the consequence yet.

Every claim about current code cites `file:line` from the tree as of 2026-08-25. Claims about
§4's own text cite `docs/ARCHITECTURE.md:line`.

---

## Calibration probes

### Probe 1 — the replay window

§4 says the delivery enumerator "reads the current outstanding set from the owner, yields it,
then yields live arrivals" (`docs/ARCHITECTURE.md:454-457`). No mechanism orders the snapshot
read against a concurrent `Publish`. Both orders fail C3:

- **Read the snapshot first, subscribe second.** A connection that opens between the read and
  the subscription misses both. It is yielded **zero** times. C3's "loses nothing that still
  matters" is false. Mechanism confirmed by the absence of any ordering rule in §4.
- **Subscribe first, read second.** A connection that opens in the window lands in the snapshot
  and in the already-subscribed channel. It is yielded **twice**. C3's "exactly once per
  enumerator" is false. The consequence is concrete: the S5 consumer starts one consume loop
  per yielded connection (`docs/ARCHITECTURE.md:190-204`), so a duplicate starts two concurrent
  `ReceiveAsync` enumerators on one single-reader channel — the payload-stealing race the
  single-reader rule exists to prevent (`Native/PlatformNearby.android.cs:116`,
  `Native/PlatformNearby.ios.cs:502` construct with `singleReader: true`). Consequence
  needs-failing-test.

The tree already holds both halves of the fix, in different places. `ChangeBroadcast`
subscribes synchronously inside `GetAsyncEnumerator`, deliberately not in an iterator body, so
the watcher is live before the caller reads state (`ChangeBroadcast.cs:93-107`) — confirmed.
`NearbyDeviceCollection` subscribes first and seeds second, and survives the duplicate because
its `Apply` is idempotent (`Devices/NearbyDeviceCollection.cs:116-128`) — confirmed.
Deliverables are not idempotent. A request is answered once and a connection is consumed once,
so the duplicate must be suppressed, not tolerated.

**Outcome: exactly once, by amendment.** The enumerator subscribes first, then reads the
snapshot through a delegate the facade injects, yields the snapshot, then yields live items and
suppresses any live item that is reference-equal to a snapshot member. The guard set is bounded
by the snapshot size and lives only inside the enumerator. No lock spans the owner and the
broadcast, so the C5 row for the underlying fact keeps its one owner. The statement
"`DeliveryBroadcast<T>` holds no state" is restated honestly: it holds no *fact* state — it
holds a per-enumerator handover guard. Feeds elements 5 and 10.

### Probe 2 — the outstanding-request fact under accept-versus-expiry

§4's S2 sequence calls the tracker twice: `Track` at arrival and `TryResolve` at the connected
callback (`docs/ARCHITECTURE.md:542,552`). Nothing runs at accept entry. Walk the race:

1. The consumer calls `request.AcceptAsync()`. The bridge arms the ledger TCS and calls
   `RespondAsync(accept)` (`docs/ARCHITECTURE.md:546-549`).
2. `InboundRequestTimeout` fires. The tracker still owns an outstanding entry for X, so it
   rejects the request, completes `Expired`, and publishes `RequestExpired`
   (`docs/ARCHITECTURE.md:560-562`).
3. The SDK receives an accept and a reject for one request. Whichever the SDK honors, the
   consumer can observe both a returned connection and a completed `Expired` task.

The fact "inbound request outstanding for X" has one owner in the table
(`docs/ARCHITECTURE.md:372`) and two deciders in the flow. That is the disease §4 exists to
cure, reproduced inside §4. Mechanism confirmed from §4's own text. Consequence
needs-failing-test.

Today's code already solves this shape with a single atomic arbiter: `AcceptAsync` runs
`_pendingRequests.TryRemove` at entry (`NearbyImplementation.cs:317`), `RejectAsync` does the
same (`NearbyImplementation.cs:347`), and the expiry body runs the same `TryRemove` after its
delay and returns early when it loses (`NearbyImplementation.state.cs:165-168`) — confirmed.
The timer disarm is secondary cleanup, not the arbiter
(`NearbyImplementation.cs:324`, `NearbyImplementation.state.cs:148-157`) — confirmed.

**Outcome: one atomic claim, by amendment.** `RequestExpiryTracker` gains
`TryClaim(deviceId)`. `AcceptAsync`, `RejectAsync`, and the expiry body each claim first, and
exactly one wins. A losing accept throws `NearbyRequestExpiredException` — the backstop §2
already decided (`docs/ARCHITECTURE.md:226-232`). A losing expiry timer returns without any
effect. The consumer can never observe both outcomes. The ledger TCS holds only the
accept-in-progress handshake, exactly as the C5 row states. The `TryResolve` call at the
connected callback is deleted — the fact was already claimed at accept entry. Feeds elements
2, 10, and 11.

### Probe 3 — join-while-calling-back at `StopAsync`

§4's teardown joins `SessionTaskSet` as step 5 and then promises "returns — initial state,
nothing stray" (`docs/ARCHITECTURE.md:584-586`). Walk the interleaving with an auto-accept task
in flight:

1. `StopAsync` disposes open connections and reaches `JoinAsync(constant bound)`.
2. The auto-accept task is awaiting its handshake. The handshake TCS lives in the bridge, and
   `StopAsync` does not dispose the bridge — only `DisposeAsync` cancels the TCS map
   (`Native/PlatformNearby.shared.cs:119-124`) — confirmed. Today nothing cancels an in-flight
   auto-accept at stop: it runs on `_disposing.Token`, which only `DisposeAsync` cancels
   (`NearbyImplementation.state.cs:206`, `NearbyImplementation.cs:398`) — confirmed.
3. The join burns its full constant bound and times out. §4 gives the timeout no observable
   form, so the task leaks silently past "nothing stray".
4. The leaked task completes later. Its `OnConnected` path writes the registry and the
   connection table after `StopAsync` cleared the registry — a write into the next session's
   state, the exact failure §3's decided item names (`docs/ARCHITECTURE.md:294-297`).
   Today's code carries the same exposure: `StopAsync` clears the registry
   (`NearbyImplementation.cs:279`) while auto-accept and the disconnect watchers are unjoined
   (`NearbyImplementation.state.cs:117,223`) — mechanism confirmed, consequence
   needs-failing-test.

A second, structural hazard: §4 does not say whether the join runs under the facade's state
gate. Today no session task takes the gate (`NearbyImplementation.state.cs:199-247` — none
waits on `_stateGate`) — confirmed. But §4's S2 view routes accept through the bridge and
draws no gate at all, so nothing stops a later implementation from creating the cycle: stop
holds the gate, the join waits on a task, the task waits on the gate.

**Outcome: order, not hope, by amendment.** The teardown sequence becomes: cancel the session
stop token → stop the pumps under the gate → dispose connections (this resolves `Disconnected`
and lets the watchers finish) → reject pending requests and disarm timers → join the task set
outside the gate → clear the registry → return. Cancellation-first makes the join fast, and the
bound is the backstop. Join-before-clear means a straggler's last transition lands before the
clear instead of after it. A join timeout is logged — the promise becomes "initial state,
nothing stray, or a logged straggler", which is the honest version. Auto-accept runs on a
session stop token that `StopAsync` cancels, not only `DisposeAsync`. Feeds elements 1, 4,
and 11.

---

## The ledger

### 1. The facade residue — pumps and auto-accept kept inline

**Attack.** Three hits. First, auto-accept is one of the session tasks C6 was written for
(`docs/ARCHITECTURE.md:283-285`), and it changes the public surface — with it enabled,
`Requests` never yields (`docs/ARCHITECTURE.md:152-153`) — yet §4 leaves it an unnamed inline
policy with no `SessionTaskSet` registration and no stop-token. Today it is a bare discard
(`NearbyImplementation.state.cs:117`) that nothing joins — confirmed. Second, auto-accept
skips expiry entirely today (`NearbyImplementation.state.cs:115-119` returns before
`ArmRequestExpiry`) — confirmed — and §4 never says whether the S2 view's `Track` step applies
to it. Third, the sequence views contradict the residue's whole justification: the map and the
layer prose keep the pumps in the facade as its "one reason to change"
(`docs/ARCHITECTURE.md:310,328-331`), but both sequence views draw the bridge writing the
registry, the tracker, and the delivery broadcast directly (`docs/ARCHITECTURE.md:542-544,
552-555`), which bypasses the pumps and gives `Native/` references to four session components.
That also erases C4's enforcement: today the SDK callback writes a channel and the pump drains
it on a thread-pool thread (`Native/PlatformNearby.shared.cs:255`,
`NearbyImplementation.state.cs:37-39`) — confirmed. A bridge that publishes directly delivers
on the SDK callback thread.

**Defense.** The residue itself is correctly sized. The pumps are the facade's one reason to
change — how public operations map onto platform streams — and the reassessment itself
declined to extract the pump machine (fix 5). Auto-accept is eight lines of policy
(`NearbyImplementation.state.cs:199-216`), and a type around it would have no independent
reason to change. The sequence views can be read as an elision of the channel hop, not a
design statement.

**Verdict: amend.** The defense wins for the residue's contents: pumps and auto-accept stay
inline. The attack wins on ownership and on the drawn dataflow. Amendments: the auto-accept
task registers in `SessionTaskSet` and runs on the session stop token. §4 states auto-accept's
contract explicitly: no `Track`, no `Requests` publish, bounded by `AcceptTimeout` (C1) rather
than `InboundRequestTimeout` (C2). Both sequence views are redrawn with the channel-and-pump
hop, and all registry and broadcast writes stay facade-side.

### 2. `RequestExpiryTracker`

**Attack.** Probe 2 in full: the claim step is missing, so the fact has two deciders in the
flow. Also a mechanical defect: the class view declares `Track(request)`
(`docs/ARCHITECTURE.md:434`) and the sequence view calls
`Track(request, InboundRequestTimeout)` (`docs/ARCHITECTURE.md:542`) — confirmed
contradiction. Also unstated: who performs the expiry effects. If the tracker rejects, writes
the registry, and publishes, it duplicates the facade's mutation path and the sibling question
"who calls `Registry.Update`" returns.

**Defense.** The component has direct ancestry (reassessment fix 5) and a real single fact to
own. Extracted, it is testable on `net10.0` with a `FakeTimeProvider`, which today's
`ExpireRequestAfterAsync` (`NearbyImplementation.state.cs:159-197`) is not in isolation. C2
needs a named enforcement point, and this is it.

**Verdict: amend.** The tracker survives with a sharpened contract: it owns the outstanding
set and its timers, exposes `Track(request)` and atomic `TryClaim(deviceId)`, and is
constructed with the options snapshot, the `TimeProvider`, and an `onExpired` delegate the
facade injects. Expiry effects — reject, `Expired` completion, registry transition, change
publish — run inside the facade-owned delegate, so device-state mutation keeps one path. The
arity contradiction resolves to `Track(request)`.

### 3. `DiscoveryRefreshLoop`

**Attack.** The class view gives the loop two fields and no members
(`docs/ARCHITECTURE.md:437-440`). Its real job today is facade surgery: each tick takes the
facade's state gate, checks the discovery flag, begins a registry generation, stops and
restarts the discover pump, and awaits the new `started`
(`NearbyImplementation.state.cs:283-325`) — confirmed. A component that reaches into another
component's gate and pumps is not "testable alone", and §4 does not say how the reach happens.

**Defense.** Direct ancestry (reassessment fix 5). The loop genuinely owns three things nothing
else owns: the interval, the settle window, and eviction
(`NearbyImplementation.state.cs:283-342`). The pump restart does not need facade internals —
it needs one operation, which the facade can hand over as a delegate, the same construction
shape element 2 uses. Eviction goes through the registry's own API
(`BeginGeneration`/`EvictUnconfirmed`, `Devices/NearbyDeviceRegistry.cs:234-263`), which is
mutation through the owner, not a second owner.

**Verdict: amend, narrowly.** The component holds. The class view gains its real members —
`Start()`, `CancelAsync()`, `DrainAsync()` — and the construction note: the facade injects one
refresh delegate that stops and restarts the discover pump under the facade's gate. The gate
never leaves the facade.

### 4. `SessionTaskSet`

**Attack.** Probe 3 in full: the join can only time out against an uncancelled task, the
timeout is invisible, and the join's position against the gate and the registry clear is
undrawn. Also undefined: `Add` after a join has started — §3 decided both `StopAsync` and
`DisposeAsync` join the set (`docs/ARCHITECTURE.md:294-297`), and a platform callback can
start a watcher mid-stop. Also the thinness question: a list plus `Task.WhenAll` plus a bound
is under twenty lines — is this a component or a helper?

**Defense.** C6's enforcement text names the type: "the two owning types. A bare `_ =` discard
of live work outside them is the review flag" (`docs/ARCHITECTURE.md:275`). A review flag
needs a nameable home, and the platform side's precedent (`KeyedSerialQueue`,
`Native/KeyedSerialQueue.cs`) shows the shape earns its keep. The census is real: three
unowned session-side discards exist today (`NearbyImplementation.state.cs:117,143,223`) —
confirmed.

**Verdict: amend.** The type survives — a contract's enforcement point is allowed to be small.
Defined semantics land in the shape: tasks self-remove on completion, `Add` during a join is
accepted and the join loops until the set is quiet or the bound elapses, a join timeout is
logged, and both `StopAsync` and `DisposeAsync` join after connection disposal and before the
registry clear, outside the state gate.

### 5. `DeliveryBroadcast<T>`

**Attack.** Probe 1 in full: the handover window makes C3 false in both orders. Defect two:
the connections snapshot source does not exist — the class view's `IPlatformNearby` exposes
`TryGetConnection(deviceId)` and no enumeration (`docs/ARCHITECTURE.md:463-471`), so "reads
the current outstanding set from the bridge's connection table"
(`docs/ARCHITECTURE.md:454-456`) names a member the seam does not have. Confirmed
contradiction. Defect three, false uniformity: the two instances differ in owner, layer, and
lifetime — requests replay from a session component, connections replay from across the
platform seam — and the generic cannot name either.

**Defense.** The type is the internal mirror of §2's doctrine, and that is cohesion, not
uniformity for its own sake: `ChangeBroadcast` carries state deltas and must not replay
(`docs/ARCHITECTURE.md:110`), `DeliveryBroadcast` carries deliverables and must replay
(`docs/ARCHITECTURE.md:111`). Folding replay into `ChangeBroadcast` as an option would put a
doctrine violation one flag away. The two instances share every mechanic — subscribe,
snapshot, dedupe, live — and differ only in the snapshot delegate the facade wires, which is
exactly what a type parameter plus one constructor argument expresses. The asymmetry of owners
is real but lives in the facade's wiring, not in the type.

**Verdict: amend.** The type survives with three changes. The handover rule from probe 1 —
subscribe, snapshot, dedupe by reference, then live. A snapshot delegate injected per
instance: the tracker's outstanding set for requests, and for connections a new
`IPlatformNearby.SnapshotConnections()` member that closes the seam gap. The prose "holds no
state of its own" is corrected to "holds no fact state — a per-enumerator handover guard
only."

### 6. The bridge

**Attack.** Four hits. First, the layering contradiction from element 1: both sequence views
give the bridge write access to the registry, the tracker, and the broadcast
(`docs/ARCHITECTURE.md:542-544,552-555`), against the map that puts those under the facade
(`docs/ARCHITECTURE.md:310-316`). If taken literally it inverts the dependency direction,
breaks C4's threading, and couples `Native/` to the session layer — the migration cost §4
exists to avoid. Second, the table type: the class view says
`Map~deviceId, NearbyConnection~` (`docs/ARCHITECTURE.md:474`) while the adopted amendment
says the table maps to the pair and claims the views were updated
(`docs/ARCHITECTURE.md:659-661`). Confirmed contradiction. Third, S8 has no home: §2 promises
the stream's name travels in-band on Android through "its existing control-message codec"
(`docs/ARCHITECTURE.md:165-172`), but the codec §2 references is iOS-only and Disconnect-only
today (`Connections/ControlMessage.cs:3-6`, encoded at `Native/PlatformNearby.ios.cs:519-520`,
decoded at `Native/PlatformNearby.ios.cs:567` — no Android call site) — confirmed. §4 never
places the codec, `OnPayload`, or `OnDisconnected` in any flow. Fourth, size: channels,
ledger, table, staging, queue, release order, and six hoisted behaviors in "one sealed class"
risks rebuilding the too-few-named-parts disease inside one file.

**Defense.** One non-partial class is the fix for the sibling-parity defect class, and that
class is smaller than it looks: the six hoisted behaviors are each a few lines, the release
order already exists as shared code (`Native/PlatformNearby.events.cs:185-200`), and the
`net10.0` partial is deleted outright. The sequence views' registry arrows are elisions of the
channel hop, not intent — the channels are drawn in the bridge's own responsibility list
(`docs/ARCHITECTURE.md:316`).

**Verdict: amend.** The bridge survives with its boundary stated as a rule: the bridge never
calls a session component — its only upward path is its channels, which the facade's pumps
drain, which is what makes C4 true. The table is typed as
`deviceId → (NearbyConnection, IPlatformConnection)` everywhere. S8 ownership is split
honestly: each adapter owns how a stream's name travels (a new in-band frame on Android, the
native carrier on iOS), and the bridge owns the platform-neutral contract that an inbound
stream arrives as a name-and-stream pair. The frame format is a wire contract between peers of
different plugin versions and goes to the decision list. `IPlatformNearby.ReleaseConnectionAsync`
stays — the facade still releases by device id — and internal release disposes the pair's
platform connection locally, which is what the adopted amendment meant.

### 7. `IPlatformAdapter`

**Attack.** The strongest available attack is migration cost: today the platform layer is one
partial class across seven files, two of them 816 and 801 lines
(`Native/PlatformNearby.android.cs`, `Native/PlatformNearby.ios.cs`), and §4 does not say
whether an incremental route exists. If the only route is one cutover of both files, the
maintainer must price a big-bang.

**Defense.** The interface passes the interface-first bar with margin — Android, iOS, a
throwing `net10.0` adapter, a scripted test adapter — and the member list is today's stable
`Platform*` partial-method set (`Native/PlatformNearby.net.cs:10-40` shows the full list) —
confirmed. The scripted adapter's value is concrete and not covered by `FakeNearby`:
`FakeNearby` doubles `IPlatformNearby`, so everything below that seam is unreachable from unit
tests today because the `net10.0` start delegates throw
(`Native/PlatformNearby.net.cs:21-25`), which makes `Step`'s success path — the channel swap
at `Native/PlatformNearby.shared.cs:236-237,66,78` — off-device-only. With a scripted adapter,
unit tests reach the swap, the three-source cancellation split in `AwaitHandshakeAsync`
(`Native/PlatformNearby.shared.cs:174-193`), and the drain-then-release order
(`Native/PlatformNearby.events.cs:185-200`). And an incremental route exists: (1) declare the
interface as a mirror of the `Platform*` list, (2) per platform, move the `Platform*` bodies
into an adapter class in the same TFM and leave the partials as one-line forwards, (3) hoist
the six duplicated behaviors into the shared class one behavior per commit, with the device
suite green after each, (4) move the SDK-facing `On*` bodies into the adapters, (5) collapse
`PlatformNearby` to the sealed non-partial bridge and delete `net.cs`. Every intermediate
commit builds all three TFMs.

**Verdict: holds.** The seam is the load-bearing element of §4 and the attack does not land: the
big-bang fear is avoidable, and the route is recorded in the shape. The residual cost — churn
in two large files, and the first cut of the contract possibly wrong — is priced in the
decision list, not hidden.

### 8. `IPlatformConnection`

**Attack.** The element has no ancestry — it appears in neither `ARCHITECTURE-REVIEW.md` nor
`ARCHITECTURE-REASSESSMENT.md` — so it must earn its place on this pass alone. Interface-first:
the throwing `net10.0` adapter never produces one, so the count is not four. Pass-through risk:
`NearbyConnection` already carries injected send and dispose delegates, so a second
per-connection object could be indirection stacked on indirection.

**Defense.** Three real implementations remain: the Android connection, the iOS connection,
and the scripted connection, which is what makes send-path faults and stalls testable on
`net10.0`. The object removes work rather than adding it: today every send re-resolves the
native handle from a device-id-keyed map on each call, and the object captures the handle once
at establishment. It is also where story S8 lands: `OpenStreamAsync(name)` on the connection
mirrors `QuicConnection` and `NWConnection` (`docs/ARCHITECTURE.md:661-663`), and without the
object S8 needs another device-id-keyed adapter member. The bridge wires `NearbyConnection`'s
existing delegates to the object — one hop, unchanged depth.

**Verdict: holds.** With one bookkeeping correction charged to element 6: the connection table
is pair-typed in every view, so the object's lifetime is visible in the C5 table. Adoption
timing is the maintainer's call and sits in the decision list, because the object is internal
and could land after 1.0 — at the cost of touching S8 twice.

### 9. The asymmetric seam

**Attack.** The claim "the device tests keep their entry points"
(`docs/ARCHITECTURE.md:358-359`) is imprecise. The entry points the device tests drive today
take SDK types — `OnConnectionResult(string, ConnectionResolution)`
(`Native/PlatformNearby.android.cs:96`), `OnPeerStateChanged(MCPeerID, MCSessionState)`
(`Native/PlatformNearby.ios.cs:489`) — confirmed. The bridge's drawn `On*` members take
translated types (`docs/ARCHITECTURE.md:477-480`). SDK-typed members therefore move into the
adapter classes, and the device suite must construct adapter-plus-bridge pairs instead of one
`PlatformNearby`. A second cost: a future backend implements the compiler-checked outbound
interface but must also learn the bridge's concrete inbound methods, which no compiler checks.

**Defense.** The asymmetry has explicit ancestry: the reassessment adapted R-5 precisely to
avoid a second interface with one caller and one implementation per direction, and §4 restates
that argument (`docs/ARCHITECTURE.md:522-526`). The inbound surface is the platform event
contract, and its executable specification is the device suite — a compiler check would
duplicate what the tests already enforce. The entry-point move is mechanical: signatures are
kept, the hosting type changes, and the suite already constructs its platform per test
(`await using var platform`, per `AGENTS.md`).

**Verdict: holds.** With the sentence corrected in the shape: the SDK-typed entry points
survive in signature and relocate into the adapter types, and the device tests construct the
adapter-bridge pair. The relocation is one mechanical commit inside migration step 4 and is
priced in the decision list.

### 10. The C5 fact-ownership table

**Attack.** Row by row. Row 1 omits `ChangeBroadcast`, a named component with no fact row.
Row 2's owner is right but the fact's readers cannot reach it — see element 5 — and the value
type is wrong once the pair lands. Row 3 collapses a fact that has six writer sites today:
`Step` resolves or faults it (`Native/PlatformNearby.shared.cs:247,251`) and the facade's
pumps set result, cancel, or exception on it
(`NearbyImplementation.state.cs:42,46,52,71,75,81`) — confirmed. Saying "Bridge resolves it"
does not make five of those sites disappear. Row 4 survives probe 2 only with the claim
amendment, and says nothing about auto-accept, which never creates the fact
(`NearbyImplementation.state.cs:115-119`) — confirmed. Rows 5–7 stand. Two facts have no row
at all: the set of live session tasks (the C6 fact `SessionTaskSet` exists to own), and how a
stream's name travels (the S8 fact with no owner anywhere in §4). The reassessment counted six
co-owned facts in today's code — the table must show where each lands, and as written it
covers four.

**Defense.** The table's frame is right, and most rows survive contact: peer identity, staging,
and options are clean single-owner rows, and row 2 is the whole point of fix 4. The row-3
problem is real but bounded: `TrySet*` idempotence means late duplicate writes are harmless
today, and the pumps' writes are a backstop for a pump that dies before `Step` runs — without
it, `StartAdvertisingAsync` awaits a signal nobody will complete.

**Verdict: amend.** The header is sharpened to match what the table can actually promise: one
owning component holds and serializes each fact, mutators route through the owner's API, and
everyone else reads or derives. Row changes: the registry row names its own broadcast. The
connection row is pair-typed and gains `SnapshotConnections()` as the read path. The started
row names the bridge as the single resolver and records the pump's fault-backstop as a
documented exception with a decision-list entry. The outstanding-request row states the atomic
claim and the auto-accept bypass. Two rows are added: live session tasks
(owner `SessionTaskSet`) and stream-name carriage (owner: each platform adapter). A closing
note records that the delivery streams own no fact — per probe 1.

### 11. The two sequence views

**Attack.** The S2 view contradicts the component map (element 1), omits the claim step
(probe 2), and carries the `Track` arity defect. The teardown view joins before rejecting
nothing — it never rejects pending requests at all, though today's `StopAsync` does
(`NearbyImplementation.cs:266-276`) — and its final promise is falsifiable (probe 3). Three
interleavings the views do not draw: a platform callback that arrives during teardown, an
accept that races expiry, and a delivery enumeration that races disposal. §4's method says
sequence views exist to show "the two flows that cross every boundary"
(`docs/ARCHITECTURE.md:398-400`), and the undrawn interleavings are where every prior defect
in this codebase lived.

**Defense.** Two views is the right count — a view per failure would be a debate log, not a
design. The failure glosses under each view (`docs/ARCHITECTURE.md:559-562,589-591`) show the
form works: prose insets carry the failure paths without a third diagram.

**Verdict: amend.** Both views are redrawn: S2 with the channel-and-pump hop, the claim at
accept entry, facade-side registry and broadcast writes, and the two-stage request assembly
(bridge builds the respond core, the facade attaches the session effects at pump time — the
consumer never calls the facade directly, so the request object carries the facade's
continuation). Teardown per probe 3, with the reject step restored. The three undrawn
interleavings land as a third prose inset, not a third diagram: late callbacks fail soft in
the bridge (a cleared ledger ignores `TrySet*`, an unmatched `WritePayload` logs and drops —
today's behavior at `Native/PlatformNearby.events.cs:209-214`), the accept-expiry race is
settled by the claim, and a delivery enumerator that is disposed mid-yield unsubscribes in
`DisposeAsync` exactly as `ChangeBroadcast` does (`ChangeBroadcast.cs:145-157`).

---

## Verdict summary

| # | Element | Verdict |
|---|---|---|
| 1 | Facade residue | amend — ownership and dataflow, contents kept |
| 2 | `RequestExpiryTracker` | amend — atomic claim, injected effects |
| 3 | `DiscoveryRefreshLoop` | amend — members and delegate drawn, component kept |
| 4 | `SessionTaskSet` | amend — join semantics defined |
| 5 | `DeliveryBroadcast<T>` | amend — handover rule, snapshot delegate, seam member |
| 6 | Bridge | amend — boundary rule, pair table, S8 ownership |
| 7 | `IPlatformAdapter` | **holds** — incremental route recorded |
| 8 | `IPlatformConnection` | **holds** — pair typing charged to element 6 |
| 9 | Asymmetric seam | **holds** — entry-point sentence corrected |
| 10 | C5 table | amend — two rows added, three rows sharpened |
| 11 | Sequence views | amend — both redrawn, third inset added |

No element falls. The premise — named components, one seam, asymmetric inbound — survives
every attack. What fell was the connective tissue: the drawn dataflow, the race arbiters, and
the table's coverage.

## Re-run traces against the amended shape

### Contracts to enforcers — exactly one each

| Contract | Enforcer | Form |
|---|---|---|
| C1 Termination | Bridge — `AwaitHandshakeAsync` plus the started signal | One shared helper owns every platform-callback deadline. |
| C2 Liveness | `RequestExpiryTracker` | Owns the timer and the atomic claim. |
| C3 Replay | `DeliveryBroadcast<T>` | The handover rule: subscribe, snapshot, dedupe, live. |
| C4 Threading | Channel construction sites | Bridge channels, `ChangeBroadcast.Subscribe`, `DeliveryBroadcast.Subscribe` — plus the rule that the bridge never calls upward. |
| C5 One owner | The amended fact table | Review artifact. |
| C6 Owned work | `SessionTaskSet` and `KeyedSerialQueue` | The two owning types. |
| C7 Drain, then release | Prose plus the bridge's release order | Local disposal of the connection pair, bounded drains, teardown order from probe 3. |

### Stories to components

| Story | Path |
|---|---|
| S1 Guest pairs | Facade `ConnectAsync` → bridge handshake → adapter. |
| S2 Host confirms | Adapter → bridge channel → pump → tracker, registry, delivery broadcast → request continuation → bridge → adapter. |
| S3 Zero-ceremony | Facade auto-accept policy, task owned by `SessionTaskSet`, bounded by `AcceptTimeout`. |
| S4 Send | `NearbyConnection` → `IPlatformConnection`, handle captured once. |
| S5 Receive | `Connections` → `DeliveryBroadcast` with the handover rule. |
| S6 Bind | Registry and its broadcast → `NearbyDeviceCollection<TRow>`. |
| S7 End | Facade `DisconnectAsync`/`StopAsync` → probe-3 teardown order. |
| S8 Stream | `OpenStreamAsync` on the connection object → adapter-owned name carriage → bridge assembles the name-and-stream pair → connection receive channel. |

### Facts to writers

Every row of the amended table has one owning component. The two previously homeless facts —
live session tasks and stream-name carriage — have owners. The delivery streams own nothing.
The started signal keeps one resolver and one documented backstop, flagged for the maintainer.

## Naming checks

The amendments add internal members and internal types only: `TryClaim`,
`SnapshotConnections()`, and no renames of existing types. No public surface changes, so the
PublicAPI baselines are untouched. The six mechanical checks in `.claude/rules/naming.md` §10
were re-run on 2026-08-25 against the current tree and pass — no vendor types under
`PublicAPI/`, no banned public type names, no public types in `Native/`, no `Nearby`-prefixed
methods, no platform identifier vocabulary outside its partials, all three baselines
identical. The `peerId` grep matches only stale generated XML under `bin/` and `obj/` — the
source uses `deviceId` (`Native/PlatformNearby.events.cs:99-101`). `SessionTaskSet` uses "session" in this repo's own sense (`AGENTS.md` uses
"session-owned tasks"), is internal, and never touches the public surface, where the word is
banned.

## Decision list

Choices only the maintainer can make. One question each, with a recommendation and the cost of
a post-1.0 decision.

1. **Migration route for D5.** Incremental strangler (the five steps in element 7) or one
   cutover of the two largest files? Recommendation: incremental — every commit builds all
   three TFMs and keeps the device suite green. Cost after 1.0: none to consumers, but every
   feature that lands in the partials before the migration (S8, `Requests`) is churn paid
   twice.
2. **`IPlatformConnection` timing.** Adopt with D5, or defer past 1.0? Recommendation: adopt
   with D5 — S8's `OpenStreamAsync` needs the home, and a deferral touches S8's platform code
   twice. Cost after 1.0: low in isolation (internal type), high if S8 ships first.
3. **The in-band frame format is a wire contract.** S8's stream name travels in-band on
   Android between peers that may run different plugin versions. The current frame is 5 bytes,
   iOS-only, Disconnect-only (`Connections/ControlMessage.cs`). Settle the extended layout
   (length-prefixed name record, version-tolerant type byte) before the first release that
   ships S8. Recommendation: reserve the layout pre-1.0. Cost after 1.0: a changed frame
   breaks cross-version peers silently — the highest deferred cost on this list.
4. **`StopAsync` cancels session tasks.** The probe-3 order gives stop a session stop token
   that cancels in-flight auto-accept. Today stop does not cancel it
   (`NearbyImplementation.state.cs:206`). Recommendation: adopt — it is what makes §3's
   "stop joins the set" decision true rather than aspirational. Cost after 1.0: none to the
   public surface, but the leaked-task write into a next session remains latent until then.
5. **The started-signal backstop.** Accept one resolver (bridge `Step`) plus one documented
   pump-failure backstop, or redesign the start protocol for a single writer? Recommendation:
   accept the documented backstop — `TrySet*` idempotence bounds the harm, and a single-writer
   redesign buys no consumer-visible behavior. Cost after 1.0: none — internal either way.
6. **Device-test relocation.** The SDK-typed entry points move into the adapter types with
   signatures kept (element 9). Accept one mechanical test-edit commit inside migration
   step 4. Recommendation: yes. Cost after 1.0: identical — the edit is the same size whenever
   it happens.

### Open questions preserved, not settled

- Final package name and the rename gate — open, per `DESIGN-PRINCIPLES.md`.
- iOS `StartFailureGraceWindow` — open, per `DESIGN-PRINCIPLES.md`. The amended shape moves
  the window's owner into the iOS adapter but changes nothing about the open question itself.
