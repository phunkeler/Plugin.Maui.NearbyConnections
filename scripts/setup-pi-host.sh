#!/usr/bin/env bash
# One-time setup for the Raspberry Pi host.
# Run once after a fresh Ubuntu Server install, before starting Docker.
# Must be run as a non-root user with sudo access.
set -euo pipefail

echo "Installing ADB..."
sudo apt-get update -q
sudo apt-get install -y android-tools-adb

echo "Adding $USER to plugdev group..."
sudo usermod -aG plugdev "$USER"

echo "Writing udev rules..."
# Add a line per manufacturer if you have devices from multiple vendors.
# Find the vendor ID for an unknown device via: lsusb (first 4 hex digits before the colon)
echo 'SUBSYSTEM=="usb", ATTR{idVendor}=="04e8", MODE="0666", GROUP="plugdev"' \
    | sudo tee /etc/udev/rules.d/51-android.rules
sudo chmod a+r /etc/udev/rules.d/51-android.rules
sudo udevadm control --reload-rules
sudo udevadm trigger

echo "Installing ADB systemd service (listens on all interfaces for Docker access)..."
sudo tee /etc/systemd/system/adb.service > /dev/null << 'EOF'
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
sudo systemctl start adb

echo ""
echo "Done. Replug any connected Android devices, then run: adb devices -l"
echo "If this is your first login after setup, log out and back in for group changes to take effect."
