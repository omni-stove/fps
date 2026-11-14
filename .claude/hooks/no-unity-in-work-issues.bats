#!/usr/bin/env bats

setup() {
  HOOK="$BATS_TEST_DIRNAME/no-unity-in-work-issues.sh"
}

bash_cmd() {
  jq -n --arg c "$1" --arg d "${2:-/repo}" '{tool_name: "Bash", cwd: $d, tool_input: {command: $c}}' | bash "$HOOK"
}

denied() {
  [ "$status" -eq 0 ]
  [ "$(printf '%s' "$output" | jq -r .hookSpecificOutput.permissionDecision)" = "deny" ]
}

passed() {
  [ "$status" -eq 0 ]
  [ -z "$output" ]
}

@test "Unity runs inside a work-issues worktree are denied" {
  for c in \
    "unity test --mode EditMode" \
    "mise run test-unity" \
    "make smoke" \
    "/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ." \
    "\"\$UNITY\" -batchmode -runTests -testPlatform EditMode"; do
    run bash_cmd "cd /repo/.git/work-issues/618 && $c"
    denied
    run bash_cmd "$c" /repo/.git/work-issues/618
    denied
  done
}

@test "dotnet tests, lint and git inside a work-issues worktree pass" {
  for c in "mise run test" "mise run lint" "make test" "git commit -m x" "grep -rn unity_ Assets" "cat Assets/game/Scripts/UnityBridge.cs"; do
    run bash_cmd "cd /repo/.git/work-issues/618 && $c"
    passed
  done
}

@test "Unity runs outside a work-issues worktree pass" {
  run bash_cmd "mise run test-unity" /repo
  passed
  run bash_cmd "unity test --mode EditMode" /repo/.claude/worktrees/x
  passed
}
