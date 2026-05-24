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
sudo tee /etc/systemd/system/adb-server.service > /dev/null << EOF
[Unit]
Description=ADB Server
After=multi-user.target

[Service]
Type=simple
User=${USER}
ExecStartPre=-/usr/bin/adb kill-server
ExecStart=/usr/bin/adb -a -P 5037 nodaemon server start
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable adb-server
sudo systemctl start adb-server

echo "Configuring firewall (UFW)..."
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow ssh
sudo ufw allow 4723/tcp
sudo ufw --force enable

echo "Disabling SSH password authentication..."
sudo sed -i 's/^#*PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
sudo systemctl restart ssh

echo "Enabling automatic security updates..."
sudo apt-get install -y unattended-upgrades
sudo dpkg-reconfigure -plow unattended-upgrades

echo "Installing fail2ban..."
sudo apt-get install -y fail2ban
sudo systemctl enable fail2ban
sudo systemctl start fail2ban

echo ""
echo "Done. Replug any connected Android devices, then run: adb devices -l"
echo "If this is your first login after setup, log out and back in for group changes to take effect."
