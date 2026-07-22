#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
    echo "Usage: bash scripts/release.sh <version>"
    echo "  e.g. bash scripts/release.sh 0.3.0-preview.1"
    exit 1
fi

TAG="v${VERSION}"

# Must be on main
BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$BRANCH" != "main" ]]; then
    echo "ERROR: You must be on main before tagging (currently on '$BRANCH')."
    echo "  Squash merge your PR first, then: git checkout main && git pull"
    exit 1
fi

# main must be up to date with origin
git fetch origin main --quiet
LOCAL=$(git rev-parse HEAD)
REMOTE=$(git rev-parse origin/main)
if [[ "$LOCAL" != "$REMOTE" ]]; then
    echo "ERROR: main is not up to date with origin/main. Run: git pull"
    exit 1
fi

# Working tree must be clean
if [[ -n "$(git status --porcelain)" ]]; then
    echo "ERROR: Working tree is dirty. Commit or stash changes first."
    exit 1
fi

# Tag must not already exist
if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo "ERROR: Tag '$TAG' already exists."
    exit 1
fi

echo "Tagging $TAG on $(git rev-parse --short HEAD) (main)"
git tag "$TAG"
git push origin "$TAG"
echo "Done — publish workflow is running: https://github.com/phunkeler/Plugin.Maui.NearbyDevices/actions"
