#!/usr/bin/env bash
set -euo pipefail

# Fetch a short-lived registration token via gh CLI (authenticated on the host
# via 'gh auth login' — no long-lived PAT stored in the image or .env).
REG_TOKEN=$(gh api \
    --method POST \
    "repos/${REPO_OWNER}/${REPO_NAME}/actions/runners/registration-token" \
    --jq '.token')

if [[ -z "$REG_TOKEN" || "$REG_TOKEN" == "null" ]]; then
    echo "ERROR: Failed to fetch runner registration token. Run 'gh auth login' on the Pi host."
    exit 1
fi

# Remove runner registration on shutdown so the runner list stays clean.
cleanup() {
    echo "Deregistering runner..."
    ./config.sh remove --token "${REG_TOKEN}" --unattended 2>/dev/null || true
}
trap 'cleanup; exit 130' INT
trap 'cleanup; exit 143' TERM

./config.sh \
    --url "https://github.com/${REPO_OWNER}/${REPO_NAME}" \
    --token "${REG_TOKEN}" \
    --name "${RUNNER_NAME:-pi-device-farm}" \
    --labels "${RUNNER_LABELS:-self-hosted,linux,ARM64}" \
    --work "_work" \
    --ephemeral \
    --unattended \
    --replace

./run.sh &
wait $!
