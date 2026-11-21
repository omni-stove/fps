#!/bin/sh
# scripts/server-tuning.sh のテスト。一時ディレクトリに Resources とサーバーの tuning を作って照合させる。
set -eu
cd "$(dirname "$0")/.."
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# $1: テストの名前、$2: 期待する終了コード、$3: 標準エラーに出るはずの文字列 (空なら何も出ないこと)
expect() {
	status=0
	err="$(sh scripts/server-tuning.sh "$work/resources" "$work/server" 2>&1)" || status=$?
	if [ "$status" != "$2" ]; then
		printf 'FAIL %s: exit %s, expected %s\n%s\n' "$1" "$status" "$2" "$err"
		fail=1
	elif [ -z "$3" ] && [ -n "$err" ]; then
		printf 'FAIL %s: expected no output\n%s\n' "$1" "$err"
		fail=1
	elif [ -n "$3" ] && ! printf '%s' "$err" | grep -qF "$3"; then
		printf 'FAIL %s: expected output containing %s\n%s\n' "$1" "$3" "$err"
		fail=1
	fi
}

reset() {
	rm -rf "$work/resources" "$work/server"
	mkdir -p "$work/resources" "$work/server"
}

reset
printf '{ "a": 1 }\n' > "$work/resources/ping.json"
cp "$work/resources/ping.json" "$work/server/ping.json"
expect "identical copies pass" 0 ""

reset
printf '{ "a": 1 }\n' > "$work/resources/ping.json"
printf '{ "a": 2 }\n' > "$work/server/ping.json"
expect "a differing copy fails" 1 "$work/server/ping.json differs from $work/resources/ping.json"

reset
printf '{ "startMoney": 800, "items": [] }\n' > "$work/resources/economy.json"
printf '{ "items": [] }\n' > "$work/server/economy.json"
printf '{ "words": [] }\n' > "$work/resources/chat-words.json"
printf '{ "words": ["noob"] }\n' > "$work/server/chat-words.json"
expect "server-owned files may differ" 0 ""

reset
printf '{}\n' > "$work/server/anticheat.json"
expect "a server-only file passes" 0 ""

reset
printf '{}\n' > "$work/resources/telemetry.json"
expect "a Resources-only file passes" 0 ""

reset
expect "empty directories pass" 0 ""

status=0
sh scripts/server-tuning.sh >/dev/null 2>&1 || status=$?
if [ "$status" != 0 ]; then
	printf 'FAIL the committed server-data/tuning matches Resources/Tuning\n'
	sh scripts/server-tuning.sh || true
	fail=1
fi

exit "$fail"
