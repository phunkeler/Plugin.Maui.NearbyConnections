# Contributing

## Commit messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/). Commit messages determine how the version is bumped and what appears in the changelog.

| Prefix | Effect | Example |
|---|---|---|
| `fix:` | Patch bump | `fix: handle null device in SetState` |
| `feat:` | Minor bump | `feat: add OutgoingTransferProgress event` |
| `feat!:` or `BREAKING CHANGE:` footer | Major bump | `feat!: rename SendAsync uri parameter` |
| `chore:`, `docs:`, `ci:`, `refactor:` | No bump | `docs: update iOS plist instructions` |

## Versioning

Versions are derived automatically from git tags at pack time via [MinVer](https://github.com/adamralph/minver). There is no version property in any project file.

The release workflow is:

1. Commits land on `main` following the conventional commit format above.
2. [release-please](https://github.com/googleapis/release-please) maintains an open Release PR that accumulates changes and proposes the next version.
3. Merging the Release PR creates a git tag (e.g. `v1.2.0`) and a GitHub Release with a generated changelog.
4. The `publish` workflow triggers on the tag, runs `dotnet pack`, and pushes the package to NuGet.org.

To release a pre-release version, manually create a tag in the format `v1.0.0-beta.1` — MinVer will produce the correct pre-release NuGet version from it.

## Running tests

```bash
dotnet run --project test/Plugin.Maui.NearbyConnections.UnitTests
```

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
