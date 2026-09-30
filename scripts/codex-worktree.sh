#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 <worktree-name>" >&2
  exit 2
}

[[ $# -eq 1 ]] || usage
NAME=$1
[[ $NAME =~ ^[A-Za-z0-9._-]+$ && $NAME != . && $NAME != .. && $NAME != .git ]] || {
  echo "Worktree name may contain only letters, numbers, dots, underscores, and hyphens." >&2
  exit 2
}

REPO_ROOT=$(git rev-parse --show-toplevel)
cd "$REPO_ROOT"

CENTRAL=$(command -v central || command -v jbcentral || true)
[[ -n $CENTRAL ]] || {
  echo "Could not find 'central' or 'jbcentral' on PATH." >&2
  exit 1
}
command -v jq >/dev/null || {
  echo "This script requires jq." >&2
  exit 1
}
command -v sbx >/dev/null || {
  echo "This script requires sbx on PATH." >&2
  exit 1
}

WORKTREE="../$NAME"
if [[ -e $WORKTREE ]]; then
  WORKTREE_ROOT=$(git -C "$WORKTREE" rev-parse --show-toplevel 2>/dev/null || true)
  EXPECTED_ROOT=$(cd "$WORKTREE" && pwd -P)
  if [[ -z $WORKTREE_ROOT || $WORKTREE_ROOT != "$EXPECTED_ROOT" ]]; then
    echo "Path already exists and is not a Git worktree root: $WORKTREE" >&2
    exit 1
  fi
else
  git worktree add "$WORKTREE"
fi

CONFIG_FILE="$HOME/.jetbrains-central/config.json"
if [[ -f $CONFIG_FILE ]]; then
  PORT=$(jq -r '.proxy_port // 19516' "$CONFIG_FILE")
else
  PORT=19516
fi
[[ $PORT =~ ^[0-9]+$ ]] || {
  echo "Invalid proxy_port in $CONFIG_FILE: $PORT" >&2
  exit 1
}

if "$CENTRAL" proxy start --help 2>&1 | grep -q -- '--ensure-updated'; then
  KEY=$("$CENTRAL" proxy start --ensure-updated --return-key)
else
  KEY=$("$CENTRAL" proxy start --return-key)
fi
[[ -n $KEY ]] || {
  echo "JetBrains Central did not return a proxy key." >&2
  exit 1
}

BASE_URL="http://host.docker.internal:${PORT}/wire/${KEY}/codex/openai/v1"

exec sbx run --name "$NAME" \
  -e "OPENAI_API_KEY=$KEY" \
  codex "$WORKTREE" .git -- \
  --config 'model_provider="jetbrains_central"' \
  --config 'model_providers.jetbrains_central.name="JetBrains Central"' \
  --config "model_providers.jetbrains_central.base_url=\"$BASE_URL\"" \
  --config 'model_providers.jetbrains_central.env_key="OPENAI_API_KEY"' \
  --config 'model_providers.jetbrains_central.wire_api="responses"'
