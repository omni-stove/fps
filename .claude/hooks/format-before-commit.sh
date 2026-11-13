#!/usr/bin/env bash
set -euo pipefail

input="$(cat)"
cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty' 2>/dev/null || true)"

printf '%s' "$cmd" | grep -Eq '(^|[^[:alnum:]_-])git(([[:space:]]+-[cC][[:space:]]+[^[:space:]]+)|([[:space:]]+-[^[:space:]]+))*[[:space:]]+commit([[:space:]]|$)' || exit 0
command -v make >/dev/null 2>&1 && command -v mise >/dev/null 2>&1 || exit 0

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"
# 一部の hunk だけステージしたファイルを add し直すと、残した変更までコミットに入るので外す
restage="$(comm -23 <(git diff --cached --name-only --diff-filter=ACMR -- '*.cs' | sort) <(git diff --name-only -- '*.cs' | sort))"
make format >/dev/null 2>&1 || true
[ -z "$restage" ] || printf '%s\n' "$restage" | while IFS= read -r f; do git add -- "$f"; done
