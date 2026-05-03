# Contributing

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
2. [release-please](https://github.com/googleapis/release-please) automatically maintains an open **Release PR** titled `chore(main): release x.y.z`. It updates `CHANGELOG.md` and `version.txt` as new commits land.
3. When you're ready to ship, **merge the Release PR**.
4. Merging creates a git tag (e.g. `v0.2.0`) and a GitHub Release.
5. The tag triggers the `publish` workflow → approve the `nuget` environment deployment in GitHub Actions → package is pushed to NuGet.org.

### Pre-release (beta, rc)

release-please does not manage pre-release tags. Create them manually:

```bash
git tag v0.2.0-beta.1
git push origin v0.2.0-beta.1
```

Then approve the deployment in GitHub Actions as usual.

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
