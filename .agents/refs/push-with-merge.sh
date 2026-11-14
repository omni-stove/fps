#!/usr/bin/env bash
# git push with recovery when the remote is ahead: on a non-fast-forward rejection, merge
# the upstream branch (no rebase, so a merge of main already on the branch is kept as is)
# and push again. A merge conflict leaves the merge in progress and exits 3 so the caller
# can resolve it and rerun.
#
#   .agents/refs/push-with-merge.sh -u origin <branch>     # run directly
#   . .agents/refs/push-with-merge.sh; push_with_merge ... # or source and call

# A head that is a merge commit gets an empty commit on top so a review that skips merge
# commits still runs on this push.
add_empty_commit_if_merge_head() {
  if git rev-parse -q --verify 'HEAD^2' >/dev/null; then
    git commit --allow-empty --no-verify -m "chore: trigger code review after merge"
  fi
}

push_with_merge() {
  # The push's own output is kept, and the failure kind is read from it below, so stderr is
  # captured too; LC_ALL=C keeps those messages in English, since a translated locale
  # would hide 'non-fast-forward'.
  local push_err
  if push_err="$(LC_ALL=C git push "$@" 2>&1)"; then
    [ -n "$push_err" ] && printf '%s\n' "$push_err" >&2
    return 0
  fi
  printf '%s\n' "$push_err" >&2
  # Only a rejection a merge can recover from is retried; anything else (auth, network, a
  # protected branch) is passed through. A bare `[rejected]` is not matched on purpose: every
  # local rejection prints as `! [rejected] ... (reason)`, so it would also catch
  # `(stale info)` or `(needs force)` and run a pull that cannot help.
  if ! printf '%s' "$push_err" | grep -qiE 'non-fast-forward|fetch first|tip of your current branch is behind'; then
    return 1
  fi
  echo "==> push rejected (remote ahead); merging upstream" >&2
  # The remote and branch are taken from the push arguments: a caller pushing with
  # `-u origin <branch>` has no upstream set yet when the push is rejected, so a bare
  # `git pull` would have nothing to track.
  local arg remote branch
  local positionals=()
  for arg in "$@"; do
    case "$arg" in
      -*) ;;
      *) positionals+=("$arg") ;;
    esac
  done
  remote="${positionals[0]:-origin}"
  branch="${positionals[1]:-$(git rev-parse --abbrev-ref HEAD)}"
  if ! git pull --no-rebase --no-edit "$remote" "$branch"; then
    echo "==> merge conflict: resolve the conflicts in the files above, then rerun this command to finish the merge and push." >&2
    exit 3
  fi
  add_empty_commit_if_merge_head
  git push "$@"
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  set -euo pipefail
  push_with_merge "$@"
fi
