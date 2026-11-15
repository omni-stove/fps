#!/usr/bin/env bash
# Unity を使わない専用サーバー (dotnet/Fps.Server) を docker/server/Dockerfile でイメージにする。Unity も Editor を閉じることも要らない。
# 使い方: scripts/server-image.sh [--tag <name:tag>] [--version <version>] [--platform <linux/amd64|linux/arm64>]
#   --tag       作るイメージの名前とタグ (既定は fps-server:local)
#   --version   サーバーに焼き込む版 (v0.2.0 など。既定は git describe --tags --always --dirty)
#   --platform  作るイメージのアーキテクチャ (既定は Docker が動いているマシンのもの)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

usage() {
	sed -n '3,6s/^# \{0,1\}//p' "$0"
}

fail() {
	echo "server-image: $*" >&2
	exit 1
}

tag="fps-server:local"
version=""
platform=""
while [ $# -gt 0 ]; do
	case "$1" in
	--tag | --version | --platform)
		[ $# -ge 2 ] && [ -n "$2" ] || fail "$1 requires a value"
		case "$1" in
		--tag) tag="$2" ;;
		--version) version="$2" ;;
		--platform) platform="$2" ;;
		esac
		shift
		;;
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

case "$platform" in
"" | linux/amd64 | linux/arm64) ;;
*) fail "--platform must be linux/amd64 or linux/arm64 (got $platform)" ;;
esac

command -v docker >/dev/null || fail "docker is not installed"
docker info >/dev/null 2>&1 || fail "cannot reach the Docker daemon (is Docker running?)"

if [ -z "$version" ]; then
	version="$(git describe --tags --always --dirty)" || fail "git describe failed; pass --version <version>"
fi
# サーバーが起動時に断るのと同じ規則 (Fps.Core.BuildVersion) で、イメージを作る前に確かめる
if [[ "$version" =~ ^[vV][0-9] ]]; then
	version="${version:1}"
fi
[[ "$version" =~ ^[A-Za-z0-9_][A-Za-z0-9._-]{0,31}$ ]] ||
	fail "'$version' is not a usable build version (letters, digits, . - _ only, not starting with . or -, at most 32 characters)"

if ! compgen -G "dotnet/ThirdParty/EOSSDK/Source/*" >/dev/null; then
	echo "server-image: no EOS SDK in dotnet/ThirdParty/EOSSDK; the image runs without EOS (make eos-sdk to add it)" >&2
fi

# Dockerfile.dockerignore は BuildKit でしか効かない
DOCKER_BUILDKIT=1 docker build ${platform:+--platform "$platform"} \
	--build-arg FPS_BUILD_VERSION="$version" \
	--file docker/server/Dockerfile --tag "$tag" .

echo "server-image: built $tag (version $version); run it with: docker run --rm -p 7777:7777/udp $tag"
