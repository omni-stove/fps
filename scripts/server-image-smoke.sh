#!/usr/bin/env bash
# .NET のサーバーのイメージを起動し、版を出して 0.0.0.0:7777 で待ち受け、SIGTERM で終了コード 0 で止まることを確かめる。
# 使い方: scripts/server-image-smoke.sh <image> [<version>]
#   version  渡せば、最初の行が [Version] <version> であることも確かめる
set -euo pipefail

fail() {
	echo "server-image-smoke: $*" >&2
	exit 1
}

[ $# -ge 1 ] && [ -n "$1" ] || fail "usage: $0 <image> [<version>]"
image="$1"
version="${2:-}"
name="fps-server-smoke-$$"

trap 'docker rm -f "$name" >/dev/null 2>&1 || true' EXIT
docker run --detach --name "$name" -p 7777:7777/udp "$image" >/dev/null

listening="[Net] server listening on 0.0.0.0:7777"
for _ in $(seq 1 60); do
	if docker logs "$name" 2>&1 | grep -qF "$listening"; then
		break
	fi
	if [ "$(docker inspect --format '{{.State.Running}}' "$name")" != true ]; then
		docker logs "$name" >&2 || true
		fail "the server exited before listening"
	fi
	sleep 1
done
logs="$(docker logs "$name" 2>&1)"
printf '%s\n' "$logs"
grep -qF "$listening" <<<"$logs" || fail "the server did not print '$listening' within 60 seconds"

if [ -n "$version" ]; then
	first="$(head -n 1 <<<"$logs")"
	[ "$first" = "[Version] $version" ] || fail "the first line is '$first', not '[Version] $version'"
fi

user="$(docker exec "$name" id -u)"
[ "$user" = 10001 ] || fail "the server runs as uid $user, not 10001 (fps)"

# 誰もつないでいなければ、最初の SIGTERM ですぐに終了コード 0 で止まる
docker stop --time 30 "$name" >/dev/null
code="$(docker inspect --format '{{.State.ExitCode}}' "$name")"
docker logs "$name" 2>&1 | tail -n 3
[ "$code" = 0 ] || fail "the server stopped with exit code $code after SIGTERM, not 0"

echo "server-image-smoke: $image started, listened on 0.0.0.0:7777 and stopped cleanly"
