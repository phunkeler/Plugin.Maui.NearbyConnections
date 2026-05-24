#!/usr/bin/env bash
# Combined Pi ADB + device initialisation.
# Run once after setup-pi-host.sh, or any time you need to re-establish device connections.
# Installs the ADB systemd service, waits for each device, then applies all persistent
# device settings so you never have to interact with the device screen again.
set -euo pipefail

SERIALS=("${@}")

if [[ ${#SERIALS[@]} -eq 0 ]]; then
    echo "Usage: $0 <serial1> [serial2] ..."
    echo "  e.g. $0 ABC123DEF456"
    exit 1
fi

# ── 1. ADB systemd service ────────────────────────────────────────────────────

echo "Installing ADB systemd service..."
sudo tee /etc/systemd/system/adb.service > /dev/null << EOF
[Unit]
Description=ADB Server
After=multi-user.target

[Service]
Type=forking
User=$USER
ExecStart=/usr/bin/adb -a -P 5037 start-server
ExecStop=/usr/bin/adb kill-server
Restart=on-failure

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable adb

echo "Restarting ADB server (all interfaces)..."
sudo systemctl restart adb
sleep 2

# ── 2. Wait for each device, then configure ───────────────────────────────────

for SERIAL in "${SERIALS[@]}"; do
    echo ""
    echo "Waiting for $SERIAL — replug USB cable if needed..."

    until adb -s "$SERIAL" get-state 2>/dev/null | grep -q "^device$"; do
        printf '.'
        sleep 1
    done
    echo " connected."

    echo "Configuring $SERIAL..."

    # Keep screen on while plugged in (USB + AC + wireless = 7)
    adb -s "$SERIAL" shell settings put global stay_on_while_plugged_in 7

    # Wake screen and dismiss keyguard
    adb -s "$SERIAL" shell input keyevent 26
    adb -s "$SERIAL" shell wm dismiss-keyguard

    # Disable screen lock via developer settings if permitted
    # (may be blocked by MDM — non-fatal if it fails)
    adb -s "$SERIAL" shell settings put global development_settings_enabled 1 || true
    adb -s "$SERIAL" shell locksettings set-disabled true 2>/dev/null \
        && echo "  Lock screen disabled." \
        || echo "  Lock screen disable skipped (MDM policy or PIN enforced — use Smart Lock instead)."

    echo "$SERIAL ready."
done

echo ""
echo "All devices configured. ADB service will start automatically on reboot."
