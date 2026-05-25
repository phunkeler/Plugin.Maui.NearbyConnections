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

# Remove any stale local config left by a previous run that exited without cleanup.
# config.sh refuses to proceed when .runner already exists, even with --replace.
rm -f .runner .credentials .credentials_rsaparams

# Remove runner registration on shutdown so the runner list stays clean.
cleanup() {
    echo "Deregistering runner..."
    # Use the remove-token endpoint (distinct from the registration token).
    REMOVE_TOKEN=$(gh api \
        --method POST \
        "repos/${REPO_OWNER}/${REPO_NAME}/actions/runners/remove-token" \
        --jq '.token' 2>/dev/null || echo "")
    if [[ -n "$REMOVE_TOKEN" && "$REMOVE_TOKEN" != "null" ]]; then
        ./config.sh remove --token "${REMOVE_TOKEN}" --unattended 2>/dev/null || true
    fi
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
    --replace \
    --unattended

./run.sh &
wait $!
