#!/usr/bin/env bash
# CI で、unityci/editor のコンテナの Unity にこのプロジェクトを開かせて <method> を -executeMethod で呼び、Unity の終了コードで終わる。
# 使い方: UNITY_LICENSE=<Unity_lic.ulf の中身> scripts/unity-ci.sh <method> [<Unity に足す引数>...]
#   UNITY_IMAGE  使うイメージ (既定は ProjectSettings/ProjectVersion.txt の版の unityci/editor:ubuntu-<版>-base-3)
# 自分で Editor を閉じないメソッドには -quit を足す。ログは標準出力に出す。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

fail() {
	echo "unity-ci: $*" >&2
	exit 1
}

[ $# -ge 1 ] && [ -n "$1" ] || fail "usage: $0 <method> [<extra Unity arguments>...]"
method="$1"
shift

[ -n "${UNITY_LICENSE:-}" ] || fail "UNITY_LICENSE is empty; set the UNITY_LICENSE secret (the contents of Unity_lic.ulf)"
command -v docker >/dev/null || fail "docker is not installed"

version="$(sed -n 's/^m_EditorVersion: *//p' ProjectSettings/ProjectVersion.txt | tr -d '\r')"
[ -n "$version" ] || fail "could not read m_EditorVersion from ProjectSettings/ProjectVersion.txt"
image="${UNITY_IMAGE:-unityci/editor:ubuntu-${version}-base-3}"

# ライセンスはログにもワークスペースにも残さないよう、自分だけが読める一時ディレクトリに置いてコンテナに渡す
license_dir="$(mktemp -d)"
trap 'rm -rf "$license_dir"' EXIT
(
	umask 077
	printf '%s' "$UNITY_LICENSE" | tr -d '\r' >"$license_dir/Unity_lic.ulf"
)

echo "unity-ci: running $method with $image"
status=0
docker run --rm \
	--workdir /project \
	--volume "$PWD:/project" \
	--volume "$license_dir/Unity_lic.ulf:/root/.local/share/unity3d/Unity/Unity_lic.ulf:ro" \
	"$image" \
	unity-editor -batchmode \
	-projectPath /project \
	-executeMethod "$method" \
	-logFile /dev/stdout "$@" || status=$?

# コンテナの root が書いた Library などを、後の actions/cache や git が読み書きできるようにする
docker run --rm --volume "$PWD:/project" "$image" chown -R "$(id -u):$(id -g)" /project >/dev/null 2>&1 || true

exit "$status"
