#!/usr/bin/env bash
# Pi device-farm preflight check.
# Run on the Pi host before triggering a UI-test workflow run.
# Exits 0 if everything is healthy; exits 1 with a summary of what is broken.
set -uo pipefail

PASS=0; FAIL=1
ERRORS=()
COMPOSE_FILE="$(cd "$(dirname "$0")/.." && pwd)/docker/docker-compose.yml"

ok()   { echo "  [OK]  $*"; }
fail() { echo "  [!!]  $*"; ERRORS+=("$*"); }

echo "=== Pi device-farm preflight ==="
echo

# ── 1. ADB server reachable and at least one device online ────────────────────
echo "── ADB ──────────────────────────────────────────────"
if ! adb devices 2>/dev/null | grep -q 'List of devices'; then
    fail "ADB server not responding — run: sudo systemctl restart adb-server"
else
    ok "ADB server responding"
    ONLINE=$(adb devices | awk '/\tdevice$/ { print $1 }')
    if [[ -z "$ONLINE" ]]; then
        fail "No devices in 'device' state — replug USB or accept the debugging prompt on the screen"
    else
        while IFS= read -r serial; do
            ok "Device online: $serial"
        done <<< "$ONLINE"
    fi
fi
echo

# ── 2. Docker runner container is running (not restarting) ───────────────────
echo "── Docker runner ────────────────────────────────────"
if ! docker compose -f "$COMPOSE_FILE" ps runner --format json 2>/dev/null | grep -q '"State"'; then
    # Fallback for older Docker Compose
    STATUS=$(docker compose -f "$COMPOSE_FILE" ps runner 2>/dev/null | tail -1)
else
    STATUS=$(docker compose -f "$COMPOSE_FILE" ps runner 2>/dev/null)
fi

if echo "$STATUS" | grep -qi "restarting"; then
    fail "Runner container is restarting — check: docker compose -f $COMPOSE_FILE logs runner"
elif echo "$STATUS" | grep -qi "running\|up"; then
    ok "Runner container is running"
else
    fail "Runner container is not running — start: docker compose -f $COMPOSE_FILE up -d runner"
fi
echo

# ── 3. XHarness installed in the runner image ────────────────────────────────
echo "── XHarness ─────────────────────────────────────────"
if docker compose -f "$COMPOSE_FILE" exec -T runner sh -c 'dotnet xharness --version' >/dev/null 2>&1; then
    VERSION=$(docker compose -f "$COMPOSE_FILE" exec -T runner sh -c 'dotnet xharness --version' 2>&1 | head -1)
    ok "XHarness installed: $VERSION"
else
    fail "XHarness not found in runner image — rebuild: docker build -t ghcr.io/phunkeler/nearbyconnections-runner:latest -f docker/runner/Dockerfile . && docker compose -f $COMPOSE_FILE up -d --force-recreate runner"
fi
echo

# ── 4. GitHub Actions runner registered and idle ─────────────────────────────
echo "── GitHub runner ────────────────────────────────────"
if command -v gh >/dev/null 2>&1; then
    RUNNER_JSON=$(gh api repos/phunkeler/Plugin.Maui.NearbyDevices/actions/runners 2>/dev/null || echo "")
    if [[ -z "$RUNNER_JSON" ]]; then
        fail "Could not reach GitHub API — check: gh auth status"
    else
        ONLINE_RUNNERS=$(echo "$RUNNER_JSON" | jq -r '.runners[] | select(.status=="online") | .name' 2>/dev/null || echo "")
        OFFLINE_RUNNERS=$(echo "$RUNNER_JSON" | jq -r '.runners[] | select(.status!="online") | "\(.name) [\(.status)]"' 2>/dev/null || echo "")
        if [[ -n "$ONLINE_RUNNERS" ]]; then
            while IFS= read -r name; do
                ok "Runner online: $name"
            done <<< "$ONLINE_RUNNERS"
        fi
        if [[ -n "$OFFLINE_RUNNERS" ]]; then
            while IFS= read -r entry; do
                fail "Runner offline: $entry"
            done <<< "$OFFLINE_RUNNERS"
        fi
        if [[ -z "$ONLINE_RUNNERS" && -z "$OFFLINE_RUNNERS" ]]; then
            fail "No runners registered — is the container running?"
        fi
    fi
else
    echo "  [--]  gh CLI not available on host — skipping GitHub runner check"
fi
echo

# ── Summary ───────────────────────────────────────────────────────────────────
echo "═════════════════════════════════════════════════════"
if [[ ${#ERRORS[@]} -eq 0 ]]; then
    echo "  All checks passed — ready to run UI tests."
    exit $PASS
else
    echo "  ${#ERRORS[@]} check(s) failed:"
    for err in "${ERRORS[@]}"; do
        echo "    • $err"
    done
    exit $FAIL
fi
