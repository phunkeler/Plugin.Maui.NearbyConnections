#!/usr/bin/env bash
# Idempotently reapply all Pi host configuration.
# Called by the reapply-config GitHub Actions workflow via:
#   ssh sandlot@host.docker.internal < scripts/reapply-host-config.sh
# Runs as root (authorized_keys uses command="sudo bash -s").
set -euo pipefail

echo "==> apt: unattended-upgrades"
cat > /etc/apt/apt.conf.d/20auto-upgrades << 'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF

cat > /etc/apt/apt.conf.d/50unattended-upgrades << 'EOF'
Unattended-Upgrade::Allowed-Origins {
    "${distro_id}:${distro_codename}";
    "${distro_id}:${distro_codename}-security";
    "${distro_id}ESMApps:${distro_codename}-apps-security";
    "${distro_id}ESM:${distro_codename}-infra-security";
};
Unattended-Upgrade::Package-Blacklist {};
Unattended-Upgrade::Remove-Unused-Kernel-Packages "true";
Unattended-Upgrade::Remove-New-Unused-Dependencies "true";
Unattended-Upgrade::Remove-Unused-Dependencies "true";
Unattended-Upgrade::Automatic-Reboot "false";
Unattended-Upgrade::SyslogEnable "true";
Unattended-Upgrade::MinimalSteps "true";
EOF

echo "==> SSH hardening"
cat > /etc/ssh/sshd_config.d/99-hardening.conf << 'EOF'
PasswordAuthentication no
PermitRootLogin no
EOF
systemctl reload ssh

echo "==> udev: Android USB rules"
cat > /etc/udev/rules.d/51-android.rules << 'EOF'
SUBSYSTEM=="usb", ATTR{idVendor}=="04e8", MODE="0666", GROUP="plugdev"
EOF
chmod a+r /etc/udev/rules.d/51-android.rules
udevadm control --reload-rules
udevadm trigger

echo "==> systemd: adb-server"
cat > /etc/systemd/system/adb-server.service << 'EOF'
[Unit]
Description=ADB Server
After=multi-user.target

[Service]
Type=simple
User=sandlot
ExecStartPre=-/usr/bin/adb kill-server
ExecStart=/usr/bin/adb -a -P 5037 nodaemon server start
Restart=always
RestartSec=3

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable adb-server
systemctl restart adb-server

echo "==> UFW"
ufw default deny incoming
ufw default allow outgoing
ufw allow ssh
ufw allow 4723/tcp
ufw --force enable

echo "==> Service health"
for svc in adb-server fail2ban; do
    STATUS=$(systemctl is-active "$svc" 2>&1 || true)
    echo "  $svc: $STATUS"
done
TIMER=$(systemctl is-active apt-daily-upgrade.timer 2>&1 || true)
echo "  apt-daily-upgrade.timer: $TIMER"

echo ""
echo "==> Done."
