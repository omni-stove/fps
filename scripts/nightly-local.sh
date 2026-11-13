#!/usr/bin/env bash
# nightly の workflow (.github/workflows/nightly.yml) と同じ手順を、この Mac の Unity で順に回す (Editor は閉じておく)。
# 使い方: scripts/nightly-local.sh [--client | --no-client]
#   --client     曜日にかかわらず macOS クライアントもビルドする (既定は nightly と同じく JST の日曜だけ)
#   --no-client  日曜でもクライアントをビルドしない
# EditMode テスト・スモーク・クライアントのビルドは 1 つが失敗しても残りを走らせ、最後に失敗したものを並べて終了コード 1 で終わる。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

usage() {
	sed -n '3,5s/^# \{0,1\}//p' "$0"
}

fail() {
	echo "nightly-local: $*" >&2
	exit 1
}

client=""
while [ $# -gt 0 ]; do
	case "$1" in
	--client) client=1 ;;
	--no-client) client=0 ;;
	-h | --help)
		usage
		exit 0
		;;
	*)
		usage >&2
		fail "unknown argument: $1"
		;;
	esac
	shift
done

if [ -z "$client" ]; then
	if [ "$(TZ=Asia/Tokyo date +%u)" = 7 ]; then client=1; else client=0; fi
fi

command -v unity >/dev/null || fail "the unity CLI is not installed"
command -v mise >/dev/null || fail "mise is not installed; run make setup"

failed=()

step() {
	local name="$1"
	shift
	echo "nightly-local: == $name"
	if "$@"; then
		echo "nightly-local: $name passed"
	else
		echo "nightly-local: $name FAILED (exit code $?)" >&2
		failed+=("$name")
	fi
}

step "EditMode tests" mise run test-unity
step "smoke suite" mise run smoke
if [ "$client" -eq 1 ]; then
	step "macOS client build" mise run build macos
else
	echo "nightly-local: skipped the macOS client build (not Sunday in JST; pass --client to build it)"
fi

if [ "${#failed[@]}" -gt 0 ]; then
	fail "failed: ${failed[*]}"
fi

echo "nightly-local: all steps passed"
