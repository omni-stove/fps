#!/usr/bin/env bash
set -euo pipefail

input="$(cat)"
cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty' 2>/dev/null || true)"

printf '%s' "$cmd" | grep -Eq '(^|[^[:alnum:]_-])git(([[:space:]]+-c[[:space:]]+[^[:space:]]+)|([[:space:]]+-[^[:space:]]+))*[[:space:]]+commit([[:space:]]|$)' || exit 0
command -v make >/dev/null 2>&1 && command -v mise >/dev/null 2>&1 || exit 0

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"
make format >/dev/null 2>&1 || true
