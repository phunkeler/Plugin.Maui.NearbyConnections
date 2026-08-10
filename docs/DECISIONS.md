# Decisions

Settled decisions that constrain the public API or the product scope, each with the trigger that
would reopen it. They live here so they are not re-litigated from scratch — a decision whose
reasoning is invisible gets re-argued every few months.

**Do not silently reverse an entry here.** If a reversal trigger fires, say so explicitly and
record the new decision.

---

## Product scope: transfer and pairing, not background chat

**Decided 2026-08-04.**

The plugin targets **foreground, both-devices-present** interactions — file/media transfer,
pairing/handoff, bounded local exchange. It does **not** target background-surviving chat, and there
is **no auto-reconnect** (not even an opt-in helper).

**Why.** After iOS backgrounding teardown shipped, the poor reconnection story prompted a genuine
"is this plugin useful?" question. The resolution separates two claims: the reconnection story *is*
bad and *is* unfixable — MPC has no background mode or reconnect primitive, and GMS poisons
endpoints on process death; every MAUI P2P library faces this. But "therefore not useful" only
follows if the use case is a long-lived background connection. For transfer and pairing,
backgrounding means the interaction is over, so teardown-on-background is **correct behaviour
rather than a limitation**.

**Consequences.** Multi-peer is deferred, though `NearbyTopology` already exposes `Cluster`/`Star`
publicly on Android while nothing tests beyond 1:1. NearbyChat is a chat sample for a
transfer/pairing library — it demos the one shape that cannot survive a lock screen — so replacing
it with a transfer or pairing sample is also the cheapest test of whether this scope decision is
right.

**Not affected.** The MultipeerConnectivity deprecation remains the more serious long-term threat,
and Network.framework has real background support paths — some of what reads as a dead end is
specifically an *MPC* dead end, not a P2P one.

**Reversal trigger:** a concrete use case requiring a connection to survive backgrounding, weighed
against the platform limits above.

---

## `INearbyConnections` is internal

**Decided 2026-08-04.** The API restructure replaced `INearbyConnections` + `INearbyAdvertiser` +
`INearbyDiscoverer` with a single public facade. The raw platform streams are internal plumbing.

**Why the earlier "keep it public" reasoning no longer applies.** It was kept public so consumers
could mock it, because `NearbyConnection` ships a public test-double constructor that is only
meaningful against a mockable interface. That requirement did not disappear — it moved. The public
facade is the mocking seam now, and `NearbyConnection`'s test-double constructor is still public to
serve it.

**Reversal trigger:** a consumer needing raw stream access beneath the facade. Make it public
deliberately, for a named consumer — not speculatively.

---

## "Tier 1 / Tier 2" terminology is retired

**Decided 2026-08-04.** Dissolved by the decision above: with one public interface there are no
tiers to name. Every "Tier 1"/"Tier 2" occurrence was removed from README, CONTRIBUTING, and AGENTS.

The word implied two parallel, comparable levels you choose between, which never matched the actual
shape — a mandatory base with optional add-ons built on top, not an alternative to it.

**Residual, still open:** `InvitationTimeout` uses MPC's vocabulary ("invitation") and became
cross-platform in 2026-08-04's timeout work, which makes the leak more visible.
`ConnectionRequestTimeout` reads neutrally on both platforms. Deliberately deferred so the whole
public vocabulary is settled at once rather than piecemeal.

---

## Advertiser/Discoverer lifecycle duplication

**Closed 2026-08-04**, superseded by the API restructure, which deleted the duplication rather than
deduplicating it.
