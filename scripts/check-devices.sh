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

if ! ss -tlnp | grep -q '0\.0\.0\.0:5037'; then
    echo "::error::ADB server is not listening on all interfaces (0.0.0.0:5037)."
    echo "  Run: sudo systemctl restart adb"
    echo "  Then verify: ss -tlnp | grep 5037"
    exit 1
fi

echo "ADB server OK (0.0.0.0:5037)"

# ── 2. Each expected device must be in 'device' state ────────────────────────

FAILED=0
for SERIAL in "${SERIALS[@]}"; do
    STATE=$(adb -s "$SERIAL" get-state 2>/dev/null || echo "offline")
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
    adb devices -l
    exit 1
fi

echo ""
echo "All devices ready."
