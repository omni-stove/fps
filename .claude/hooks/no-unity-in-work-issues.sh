#!/usr/bin/env bash
set -euo pipefail

input="$(cat)"
cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty' 2>/dev/null || true)"
cwd="$(printf '%s' "$input" | jq -r '.cwd // empty' 2>/dev/null || true)"

printf '%s\n%s' "$cwd" "$cmd" | grep -q '/\.git/work-issues/' || exit 0
printf '%s' "$cmd" | grep -Eq '(^|[^[:alnum:]_.-])unity([[:space:]]|$)|Unity\.app|/MacOS/Unity([[:space:]]|$)|-batchmode|(mise[[:space:]]+run|make)[[:space:]]+(build|build-server|export-server-data|server-image|test-unity|smoke|perf|nightly-local)([[:space:]]|$)' || exit 0

jq -n '{hookSpecificOutput: {hookEventName: "PreToolUse", permissionDecision: "deny", permissionDecisionReason: "Check with `mise run test` and `mise run lint`; put what only Unity can show in the pr.sh body as left for a person."}}'
