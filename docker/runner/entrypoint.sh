#!/usr/bin/env bash
set -euo pipefail

# Fetch a short-lived registration token from the GitHub API.
# Requires GITHUB_PAT with 'repo' scope (or fine-grained with Administration:write).
REG_TOKEN=$(curl -fsSL -X POST \
    -H "Authorization: token ${GITHUB_PAT}" \
    -H "Accept: application/vnd.github.v3+json" \
    "https://api.github.com/repos/${REPO_OWNER}/${REPO_NAME}/actions/runners/registration-token" \
    | jq -r '.token')

if [[ -z "$REG_TOKEN" || "$REG_TOKEN" == "null" ]]; then
    echo "ERROR: Failed to fetch runner registration token. Check GITHUB_PAT scopes."
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
