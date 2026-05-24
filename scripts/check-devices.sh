#!/usr/bin/env bash
# Verifies ADB is listening on all interfaces and all expected devices are ready.
# Exits 0 if everything is healthy, 1 with a diagnostic message if not.
# Called by GitHub Actions before the test run.
set -euo pipefail

SERIALS=("${@}")

if [[ ${#SERIALS[@]} -eq 0 ]]; then
    echo "Usage: $0 <serial1> [serial2] ..."
    exit 1
fi

# ── 1. ADB must be listening on 0.0.0.0, not 127.0.0.1 ───────────────────────

ADB_HOST="${ANDROID_ADB_SERVER_HOST:-localhost}"
ADB_PORT="${ANDROID_ADB_SERVER_PORT:-5037}"
ADB="adb -H $ADB_HOST -P $ADB_PORT"

if ! $ADB devices 2>/dev/null | grep -q 'List of devices'; then
    echo "::error::Cannot reach ADB server at $ADB_HOST:$ADB_PORT."
    echo "  On the Pi host run: sudo systemctl restart adb"
    echo "  Then verify: ss -tlnp | grep 5037"
    exit 1
fi

echo "ADB server OK ($ADB_HOST:$ADB_PORT)"

# ── 2. Each expected device must be in 'device' state ────────────────────────

FAILED=0
for SERIAL in "${SERIALS[@]}"; do
    STATE=$($ADB -s "$SERIAL" get-state 2>/dev/null || echo "offline")
    if [[ "$STATE" == "device" ]]; then
        echo "Device $SERIAL OK"
    else
        echo "::error::Device $SERIAL is '$STATE' — replug USB or run init-pi-devices.sh"
        FAILED=1
    fi
done

if [[ $FAILED -eq 1 ]]; then
    echo ""
    echo "Connected devices:"
    $ADB devices -l
    exit 1
fi

echo ""
echo "All devices ready."
