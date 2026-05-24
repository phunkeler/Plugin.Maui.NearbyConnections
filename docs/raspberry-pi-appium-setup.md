# Appium Device Farm — Raspberry Pi Setup

Runs an Appium server in Docker on a Raspberry Pi (Ubuntu Server 24.04+, arm64), with two Android devices connected via USB. The test runner (`dotnet test`) executes on a separate Windows/macOS/Linux machine and targets the Pi over the LAN.

## Architecture

```
[Dev Machine]
  └── dotnet test → http://<pi-ip>:4723

[Raspberry Pi]
  ├── adb server (host, port 5037)
  ├── Docker → Appium :4723
  │     └── connects to host adb via TCP
  ├── Android Device 1 (USB)
  └── Android Device 2 (USB)
```

ADB runs directly on the Pi host so it owns the USB connections. The Appium container connects to it over `host-gateway:5037` — no USB passthrough into Docker required.

---

## Prerequisites

- Raspberry Pi 4 (4 GB+) or Pi 5 running **Ubuntu Server 24.04 arm64**
- Docker Engine installed on the Pi
- Two Android devices with **USB Debugging** enabled
- `adb` installed on the Pi host

---

## 1. Install ADB on the Pi Host

```bash
sudo apt update
sudo apt install -y android-tools-adb
adb version
```

---

## 2. Run the Pi Host Setup Script

Clone the repo onto the Pi, then run the one-time host setup script. It installs ADB, adds your user to the `plugdev` group, and installs `android-sdk-platform-tools-common` — the package [Google officially recommends](https://developer.android.com/studio/run/device#setting-up) for Android USB udev rules on Linux:

```bash
bash scripts/setup-pi-host.sh
```

Log out and back in after running the script for the group change to take effect.

---

## 3. Connect the Android Devices

Plug both devices into the Pi via USB. On each device, pull down the notification shade, tap the **USB** notification, and set the mode to **File Transfer (MTP)**.

Then:

```bash
adb start-server
adb devices -l
```

Each device should appear as `device`. If a device shows `unauthorized`, unlock it and tap **Allow** on the USB debugging prompt, then re-run `adb devices -l`.

Note the two serial numbers — you will need them for `NearbyConnections.runsettings`.

---

## 4. Create the Docker Files

Create a working directory on the Pi:

```bash
mkdir -p ~/appium && cd ~/appium
```

### `Dockerfile`

The [official `appium/appium` image](https://hub.docker.com/r/appium/appium/) is `amd64`-only and would run under QEMU emulation on the Pi — too slow for UI tests. Build a native `arm64` image instead:

```dockerfile
FROM node:lts-bookworm-slim

RUN apt-get update && apt-get install -y \
        default-jdk \
        android-tools-adb \
    && rm -rf /var/lib/apt/lists/*

# UiAutomator2 driver requires ANDROID_HOME with platform-tools/adb present.
# adb is installed via apt to /usr/bin/adb — symlink it into the expected SDK layout.
ENV ANDROID_HOME=/opt/android-sdk
ENV PATH=$PATH:$ANDROID_HOME/platform-tools
RUN mkdir -p $ANDROID_HOME/platform-tools \
    && ln -s /usr/bin/adb $ANDROID_HOME/platform-tools/adb

RUN npm install -g appium && appium driver install uiautomator2

EXPOSE 4723
CMD ["appium", "--address", "0.0.0.0", "--port", "4723"]
```

### `docker-compose.yml`

```yaml
services:
  appium:
    build: .
    ports:
      - "4723:4723"
    environment:
      - ANDROID_ADB_SERVER_HOST=host-gateway
      - ANDROID_ADB_SERVER_PORT=5037
    extra_hosts:
      - "host-gateway:host-gateway"
    restart: unless-stopped
```

`host-gateway` is Docker's built-in alias for the Pi host — no hardcoded IP needed.

---

## 5. Build and Start

```bash
cd ~/appium
docker compose build
docker compose up -d
```

Verify Appium is reachable:

```bash
curl http://localhost:4723/status
```

You should receive a JSON response with `"ready": true`.

---

## 6. Configure the Test Project

Find the Pi's LAN IP:

```bash
hostname -I
```

In `NearbyConnections.runsettings`, set the device serials noted in step 3 and point the Appium URL at the Pi:

```xml
<TestRunParameters>
  <Parameter name="DEVICE1_SERIAL" value="your-serial-1" />
  <Parameter name="DEVICE2_SERIAL" value="your-serial-2" />
  <Parameter name="APPIUM_SERVER_URL" value="http://192.168.x.pi:4723" />
</TestRunParameters>
```

---

## 7. Prepare Devices

Run the appropriate script before each test session to wake the screens, keep them on, and dismiss the lock screen.

**bash / zsh:**
```bash
export DEVICE1_SERIAL=your-serial-1
export DEVICE2_SERIAL=your-serial-2
bash scripts/prepare-devices.sh
```

**PowerShell:**
```powershell
$env:DEVICE1_SERIAL = "your-serial-1"
$env:DEVICE2_SERIAL = "your-serial-2"
.\scripts\prepare-devices.ps1
```

If Smart Lock is unavailable and a PIN is required, set `DEVICE_PIN` and uncomment the relevant lines in the script.

---

## 8. Run the Tests

From your dev machine (the test project uses `MSTest.Sdk`, which is Microsoft Testing Platform — `dotnet run` is the native invocation):

```bash
dotnet run --project test/Plugin.Maui.NearbyConnections.UiTests -- --settings NearbyConnections.runsettings
```

Filter to a single test class:

```bash
dotnet run --project test/Plugin.Maui.NearbyConnections.UiTests -- --settings NearbyConnections.runsettings --filter "ClassName=ConnectionLifecycleTests"
```

---

## Keeping ADB Connected After Reboots

Create a systemd service to reconnect devices on boot:

```bash
sudo nano /etc/systemd/system/adb-devices.service
```

```ini
[Unit]
Description=Start ADB server and verify devices
After=multi-user.target

[Service]
Type=oneshot
ExecStart=/usr/bin/adb start-server
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable adb-devices
sudo systemctl start adb-devices
```

---

## 9. GitHub Actions Self-Hosted Runner

The UI test workflow (`.github/workflows/ui-tests.yml`) targets a self-hosted runner on the Pi so CI jobs can reach the VLAN-connected devices. The runner polls GitHub over outbound HTTPS — no inbound ports or port-forwarding required.

### Create a dedicated runner user

Run the runner as a low-privilege user with ADB access only — not your personal login:

```bash
sudo useradd -m -s /bin/bash github-runner
sudo usermod -aG plugdev github-runner
```

### Register the runner

In the GitHub repo: **Settings → Actions → Runners → New self-hosted runner**.
Select **Linux / ARM64**. Copy the commands GitHub generates and run them as the new user:

```bash
sudo -u github-runner bash
mkdir ~/actions-runner && cd ~/actions-runner
# paste GitHub's download + configure commands here
# when prompted for labels, add: self-hosted,linux,ARM64
./run.sh   # verify it connects, then Ctrl+C
```

Install as a systemd service so it starts on boot:

```bash
exit   # back to your normal user
sudo /home/github-runner/actions-runner/svc.sh install github-runner
sudo /home/github-runner/actions-runner/svc.sh start
sudo /home/github-runner/actions-runner/svc.sh status
```

### Add repository secrets

**Settings → Secrets and variables → Actions → New repository secret:**

| Secret | Value |
|--------|-------|
| `DEVICE1_SERIAL` | ADB serial of device 1 — from `adb devices -l` |
| `DEVICE2_SERIAL` | ADB serial of device 2 — add when second device is available |
| `APPIUM_SERVER_URL` | `http://<pi-lan-ip>:4723` — find with `hostname -I` |

---

## 10. Security Hardening

This repo is public. Apply all of the following before the runner goes live.

### GitHub UI — Actions settings

**Settings → Actions → General:**

- **Fork pull request workflows** → *Require approval for all outside collaborators*
- **Workflow permissions** → *Read repository contents only*

### GitHub UI — Branch protection on `main`

**Settings → Branches → Add branch ruleset for `main`:**

- Require a pull request before merging
- Require review from Code Owners (`* @phunkeler` is already set in `.github/CODEOWNERS`)
- Dismiss stale reviews when new commits are pushed
- Require status checks to pass (add `UI Tests (Pi device farm)` once it has run once)

### Why these layers matter

| Threat | Mitigated by |
|--------|-------------|
| Fork PR triggers runner | Settings → Require approval for outside collaborators |
| Workflow runs for non-owner | `if: github.repository_owner == 'phunkeler'` in workflow |
| Workflow file tampered via PR | CODEOWNERS + branch protection requiring Code Owner review |
| Compromised runner escapes to Pi | Dedicated `github-runner` user with no sudo |

---

## Troubleshooting

### ADB Device Not Appearing

Work through these layers in order — each one rules out the level below it.

---

#### Step 1 — Is the device visible to the OS at all?

```bash
lsusb
```

Look for a new entry representing your phone (e.g. `Google Inc.`, `Samsung`, `QUALCOMM`). If nothing appears:

- **Try a different cable.** Most USB-C cables are charge-only with no data lines. Use one you know transfers files (e.g. one that came with the device).
- **Change USB mode on the phone.** Pull down the notification shade → tap the USB notification → select **File Transfer (MTP)**. Android defaults to "Charging only" which can prevent USB enumeration on Linux.
- **Try a different port.** If the phone is plugged into a USB hub, try directly into the Pi — some hubs don't supply enough power to enumerate phones.

Re-run `lsusb` after each change. Once the device appears, move to step 2.

---

#### Step 2 — Does ADB see it without root?

```bash
adb kill-server && adb start-server
adb devices -l
```

If the device appeared in `lsusb` but not here, test with root to confirm it's a permissions issue:

```bash
sudo adb devices -l
```

If it appears with `sudo` — it's a udev rule problem. Continue to step 3.

If it still doesn't appear with `sudo` — accept the trust prompt on the phone (tap **Allow** when asked about USB debugging), then re-run.

---

#### Step 3 — Add the udev rule for your device's vendor ID

Linux needs a udev rule to grant your user access to the USB device. You need the **vendor ID** — the first four hex digits from the device's `lsusb` line.

**Find the vendor ID on the Pi (bash/zsh):**

```bash
lsusb
# Example output:
# Bus 001 Device 003: ID 04e8:6860 Samsung Electronics Co., Ltd ...
#                        ^^^^
#                        vendor ID
```

**Find the vendor ID on Windows (PowerShell) — useful if setting up a second Pi remotely:**

```powershell
Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -match 'USB\\VID_' } |
    Select-Object FriendlyName, InstanceId |
    Format-List
# Look for your phone in FriendlyName — InstanceId will contain VID_04E8 etc.
```

Common vendor IDs:

| Manufacturer | Vendor ID |
|---|---|
| Samsung | `04e8` |
| Google / Pixel | `18d1` |
| OnePlus | `2a70` |
| Xiaomi | `2717` |
| Sony | `0fce` |
| Motorola | `22b8` |
| Huawei | `12d1` |

Write the udev rule (replace `04e8` with your vendor ID if different):

```bash
echo 'SUBSYSTEM=="usb", ATTR{idVendor}=="04e8", MODE="0666", GROUP="plugdev"' | sudo tee /etc/udev/rules.d/51-android.rules
sudo chmod a+r /etc/udev/rules.d/51-android.rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

Replug the cable, then:

```bash
adb devices -l
```

The device should now appear as `device`. If it shows `unauthorized`, unlock the phone and tap **Allow** on the USB debugging prompt.

---

### Appium / Docker Issues

| Symptom | Check |
|---------|-------|
| `curl http://localhost:4723/status` fails | `docker compose logs appium` — check for JDK or driver errors |
| Appium session times out from dev machine | Confirm port 4723 is open: `sudo ufw allow 4723/tcp` |
| UiAutomator2 can't find devices | Container can't reach host ADB — verify `host-gateway` resolves: `docker compose exec appium ping host-gateway` |

---

## Upgrading Appium

```bash
cd ~/appium
docker compose build --no-cache
docker compose up -d
```
