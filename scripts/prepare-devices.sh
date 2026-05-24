#!/usr/bin/env bash
# Prepares connected Android devices for automated testing.
# Reads DEVICE1_SERIAL and DEVICE2_SERIAL from the environment.
# Optionally reads DEVICE_PIN if Smart Lock is unavailable.
set -euo pipefail

: "${DEVICE1_SERIAL:?DEVICE1_SERIAL is not set}"
: "${DEVICE2_SERIAL:?DEVICE2_SERIAL is not set}"

SERIALS=("$DEVICE1_SERIAL" "$DEVICE2_SERIAL")

for SERIAL in "${SERIALS[@]}"; do
    echo "Preparing $SERIAL..."

    adb -s "$SERIAL" shell settings put global stay_on_while_plugged_in 7
    adb -s "$SERIAL" shell input keyevent 26
    adb -s "$SERIAL" shell wm dismiss-keyguard

    # Uncomment if Smart Lock is unavailable and a PIN is required:
    # adb -s "$SERIAL" shell input swipe 540 1600 540 800
    # adb -s "$SERIAL" shell input text "${DEVICE_PIN:?DEVICE_PIN is not set}"
    # adb -s "$SERIAL" shell input keyevent 66

    echo "$SERIAL ready."
done
