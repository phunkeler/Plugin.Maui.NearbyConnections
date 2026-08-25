# Naming and structure rules

Binding rules for `src/Plugin.Maui.NearbyConnections/`. These outrank habit and outrank a
generic .NET convention that contradicts them.

**This file states the rules. `DESIGN-PRINCIPLES.md` states why.** Read that document before you
argue with a rule here. Read `AGENTS.md` for the architecture and the build commands.

Where a rule here and verified repo evidence disagree, this file is the bug. Fix it here, in a
commit, rather than working around it.

## 1. Published identity is locked through 1.0

Do not change these values. Do not propose changing them.

| Surface | Value |
|---|---|
| `PackageId` | `Plugin.Maui.NearbyConnections` |
| `AssemblyName` | `Plugin.Maui.NearbyConnections` |
| `RootNamespace` and the public namespace | `Plugin.Maui.NearbyConnections` |
| Repository | `phunkeler/Plugin.Maui.NearbyConnections` |

`AssemblyName` and `RootNamespace` are pinned explicitly in the csproj. Leave them pinned. Both
otherwise derive from the project filename, so a project-file rename changes them silently.

Type names, file names, and folder names are **not** locked. Reorganise them freely.

### The rename gate

A final package name is executed exactly once, as a single coordinated change spanning code, the
NuGet package ID, the GitHub repository, the docs, CI, and the Sonar key. **Reject by default any
rename proposal before 1.0 that is not that one-shot coordinated change.**

## 2. The public vocabulary is vendor-neutral

Never use a vendor's term as a public type name, member name, or parameter name.

| Banned on the public surface | Whose word it is | Use instead |
|---|---|---|
| `Peer` | Apple | `Device` |
| `Endpoint` | Google | `Device` |
| `Browser`, `Advertiser` | Apple | describe the operation: `Discovery`, `Advertising` |
| `Strategy` | Google | `Topology` |
| `Invitation` | Apple | `Request`, or name what the value bounds |
| `Session` | Apple (`MCSession`) | `Nearby`, or name the capability |
| `Radio` | neither, but implies one transport | name the capability |

Internal code may use the platform's own term. `Native/PeerLookup` is correct: it is internal, and
it is the layer that talks to the SDKs.

The phrase "Nearby Connections" survives only where it names Google's actual technology.

```
GOOD  Android uses Google Nearby Connections.
BAD   Nearby Connections is the abstraction exposed by this library.
```

## 3. Public type names resist collision

A MAUI app already has `Device`, `Application`, `Connectivity`, and `Permissions` in scope. Prefix a
public type with `Nearby` when a bare name would collide.

- `NearbyDevice`, `NearbyConnection`, `NearbyPayload`, `NearbyBytesPayload`, `NearbyFilePayload`.
- `NearbyException` is the base. Every subclass is `sealed` and `Nearby`-prefixed.

The exemption is for a compound domain noun with no plausible MAUI collision — `ConnectionRole` is
the one in use. Do not widen the exemption to save characters.

**Never prefix a method with `Nearby`.** The type already carries it.

## 4. Identifiers on the public surface are this library's, not a platform's

`NearbyDevice.Id` is minted by `PeerLookup.MintDeviceId`: 8 bytes from
`RandomNumberGenerator`, rendered as 16 hex characters, identical in shape on every platform.

- **Never publish a platform identifier.** Google's endpoint id and Apple's `MCPeerID` stay inside
  `PeerLookup`. A device id is meaningless to either SDK, so translate at the edge —
  `DeviceIdFor` on the way in, `TryGetEndpointId` / `TryGetHandle` on the way out.
- **Never derive an id from peer-supplied data.** A display name collides across same-named devices
  and puts identity data into the identifier. This was a real defect: the id was once a hash of an
  archived `MCPeerID`, whose archive contains the display name.
- **Name the concept `deviceId` everywhere above `Native/`.** `peerId` is Apple-flavoured and
  `endpointId` is Google-flavoured; both name the same thing and both are wrong outside the partial
  that talks to that SDK. Inside a platform partial, `endpointId` and `peerID` are correct — they
  are that SDK's own value.

## 5. Every public async method is bounded and cancellable

- Suffix `Async`.
- Take a `CancellationToken` when the method does I/O.
- Terminate: return, throw, or observe cancellation within a bounded time on both platforms. See
  `AGENTS.md` → *Two termination guarantees*.
- Name a timeout option for what it bounds: `ConnectTimeout`, `AcceptTimeout`,
  `InboundRequestTimeout`, `TransferInactivityTimeout`.

## 6. Errors are typed exceptions

Throw `NearbyException` or one of its sealed subclasses at the public boundary. Never return `null`
to signal failure.

File each subclass under the folder its domain owns, not beside the base type.

## 7. Platform divergence is named, never hidden

A setter that silently does nothing on the current platform is a defect. Put a platform-divergent
option behind a named platform scope, so the divergence is visible at the call site.

```csharp
options.Android.Topology          // GOOD - the platform is in the expression
options.Topology                  // BAD  - silently inert on iOS
```

**All three PublicAPI baselines stay byte-identical.** That is the machine-checkable form of this
rule. An option that exists on one TFM only forces consumers into `#if`.

Document platform divergence on the member itself, in its XML docs.

## 8. `Native/` is the quarantine

- **No `public` types in `Native/`.** Public *members* of an internal type are fine — the rule is
  about the type's own accessibility.
- Platform code lives in a platform partial (`*.android.cs`, `*.ios.cs`, `*.net.cs`), never behind
  `#if` in shared logic. When shared code needs a platform-specific step, declare a `partial void`
  hook and implement it in exactly one platform file.
- `Native/` is this plugin's translation layer. `Platforms/` is the MAUI SDK's reserved folder.
  They are different things. Do not rename `Native/` to `Platform/`.

## 9. Folders state the model

```
Connections/  a connection and its lifecycle
Devices/      device identity, status, and the device set
Discovery/    availability, advertising and discovery failures
Payload/      the data
Transfer/     the act of moving it
Options/      configuration
Native/       the translation layer  (nothing public)
Platforms/    MAUI SDK convention folder
```

A payload is the data. A transfer is the act of moving it. Keep them in separate folders — collapsing
them produces an API that cannot describe a payload that failed to transfer.

The facade (`INearby.cs`), its implementation, the root exception, and the registration extensions
sit at the project root, because each spans every domain.

The unit test project mirrors this layout.

## 10. Verify before you claim

These are reproducible. Run them rather than asserting conformance.

```bash
# No vendor types on the public surface (expect no output)
grep -rE "Android\.Gms|MultipeerConnectivity|MCSession|MCPeerID" \
  src/Plugin.Maui.NearbyConnections/PublicAPI/

# No banned vocabulary as public type names (expect no output)
grep -rE "^Plugin\.Maui\.NearbyConnections\.(Nearby)?(Session|Peer|Endpoint|Browser|Advertiser|Strategy|Radio)$" \
  src/Plugin.Maui.NearbyConnections/PublicAPI/

# No public TYPES in Native/ (expect no output)
grep -rnE "^\s*public\s+(sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+|record\s+)*(class|interface|struct|enum|record|delegate)\b" \
  src/Plugin.Maui.NearbyConnections/Native/*.cs

# No Nearby-prefixed methods (expect no output)
grep -rE "\.Nearby[A-Za-z]+Async\(" src/Plugin.Maui.NearbyConnections/

# No platform identifier vocabulary outside the partial that owns it (expect no output)
grep -rn "peerId" src/Plugin.Maui.NearbyConnections/
grep -rn "endpointId" src/Plugin.Maui.NearbyConnections/ --include="*.cs" \
  | grep -v "AndroidAdapter.android.cs" | grep -v "AndroidAdapter.FileNames.android.cs" | grep -v "PeerLookup.android.cs"

# All three baselines identical (expect no output)
diff src/Plugin.Maui.NearbyConnections/PublicAPI/net10.0/PublicAPI.Unshipped.txt \
     src/Plugin.Maui.NearbyConnections/PublicAPI/net10.0-android/PublicAPI.Unshipped.txt
diff src/Plugin.Maui.NearbyConnections/PublicAPI/net10.0/PublicAPI.Unshipped.txt \
     src/Plugin.Maui.NearbyConnections/PublicAPI/net10.0-ios/PublicAPI.Unshipped.txt
```

The public API surface is build-enforced. When RS0016 fires, add the listed lines to
`PublicAPI/{tfm}/PublicAPI.Unshipped.txt`. **Never suppress the analyzer to go green.**

## 11. Open questions stay open

`DESIGN-PRINCIPLES.md` → *Open questions* lists what is deliberately undecided. Raise those. Do not
resolve one silently in a commit.
