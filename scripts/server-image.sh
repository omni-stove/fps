#!/usr/bin/env bash
# 専用サーバーの Linux 版を Unity でビルドし (make build-server linux と同じ)、docker/server/Dockerfile でイメージにする。
# 使い方: scripts/server-image.sh [--tag <name:tag>] [--version <version>] [--skip-build]
#   --tag         作るイメージの名前とタグ (既定は fps-server:local)
#   --version     サーバーに入れる版 (v0.2.0 など。既定は git describe --tags --always --dirty)
#   --skip-build  Unity でのビルドを飛ばし、すでにある Builds/server-linux/ を使う
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly server_dir="Builds/server-linux"
readonly executable="$server_dir/fps-server.x86_64"

usage() {
	sed -n '3,6s/^# \{0,1\}//p' "$0"
}

fail() {
	echo "server-image: $*" >&2
	exit 1
}

tag="fps-server:local"
version=""
skip_build=0
while [ $# -gt 0 ]; do
	case "$1" in
	--tag)
		[ $# -ge 2 ] && [ -n "$2" ] || fail "--tag requires a value"
		tag="$2"
		shift
		;;
	--version)
		[ $# -ge 2 ] && [ -n "$2" ] || fail "--version requires a value"
		version="$2"
		shift
		;;
	--skip-build) skip_build=1 ;;
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

# Unity のビルドは数分かかるので、Docker が使えないことはその前に知らせる
command -v docker >/dev/null || fail "docker is not installed"
docker info >/dev/null 2>&1 || fail "cannot reach the Docker daemon (is Docker running?)"

if [ "$skip_build" -eq 0 ]; then
	command -v mise >/dev/null || fail "mise is not installed; run make setup, or pass --skip-build to reuse $server_dir"
	if [ -n "$version" ]; then
		mise run build-server linux --version "$version"
	else
		mise run build-server linux
	fi
elif [ -n "$version" ]; then
	fail "--version has no effect with --skip-build; the version is fixed when Builds/server-linux/ is built"
fi

[ -x "$executable" ] || fail "$executable is missing; build it with make build-server linux"

# サーバーは x86_64 の Linux 向けにしかビルドしないので、Apple Silicon の Mac でも amd64 のイメージにする。
# --chmod と Dockerfile.dockerignore は BuildKit でしか効かない
DOCKER_BUILDKIT=1 docker build --platform linux/amd64 --file docker/server/Dockerfile --tag "$tag" .

echo "server-image: built $tag; run it with: docker run --rm -p 7777:7777/udp $tag"
