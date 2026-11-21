#!/bin/sh
# server-data/tuning の JSON が、同じ名前の Assets/game/Resources/Tuning の JSON と同じ中身かを確かめる。
# 両方にあるファイルは Resources を正とし、サーバーへは make export-server-rules (ServerRulesExporter) で写す。
# 引数でディレクトリを替えられるのは、scripts/server-tuning-test.sh が作ったディレクトリで確かめるため。
# 使い方: sh scripts/server-tuning.sh [<Resources/Tuning> <server-data/tuning>]
set -eu
cd "$(dirname "$0")/.."
resources="${1:-Assets/game/Resources/Tuning}"
server="${2:-server-data/tuning}"

# 名前は同じでもサーバー用に別の中身を持つファイル。ここに足すときは docs/dedicated-server.md にも理由を書く。
# economy.json: 財布の額は試合の設定 (match.economy) から読み、品はオンラインのルールの武器の一覧にある武器だけを置く。
# chat-words.json: チャットはオンラインだけなので、伏せる語はサーバーの分だけを持つ。
server_owned="economy.json chat-words.json"

is_server_owned() {
	for owned in $server_owned; do
		[ "$owned" = "$1" ] && return 0
	done
	return 1
}

fail=0
for path in "$server"/*.json; do
	[ -e "$path" ] || continue
	name="$(basename "$path")"
	[ -e "$resources/$name" ] || continue
	is_server_owned "$name" && continue
	if ! cmp -s "$resources/$name" "$path"; then
		printf '%s differs from %s; run make export-server-rules (or copy the Resources file) and commit it\n' "$path" "$resources/$name" >&2
		fail=1
	fi
done
exit "$fail"
