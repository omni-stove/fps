#!/usr/bin/env bash
# nightly の workflow から、SmokeSuite.RunAll を unityci/editor のコンテナの Unity で走らせ、その終了コードで終わる。
# game-ci/unity-builder は Unity に必ず -quit を付け、Play Mode に入る SmokeRun が走る前に Editor が閉じるので、ここで直に起動する。
# 使い方: UNITY_LICENSE=<Unity_lic.ulf の中身> scripts/nightly-smoke-ci.sh
#   UNITY_IMAGE  使うイメージ (既定は ProjectSettings/ProjectVersion.txt の版の unityci/editor:ubuntu-<版>-base-3)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

fail() {
	echo "nightly-smoke-ci: $*" >&2
	exit 1
}

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

echo "nightly-smoke-ci: running Fps.Editor.SmokeSuite.RunAll with $image"
status=0
docker run --rm \
	--workdir /project \
	--volume "$PWD:/project" \
	--volume "$license_dir/Unity_lic.ulf:/root/.local/share/unity3d/Unity/Unity_lic.ulf:ro" \
	"$image" \
	unity-editor -batchmode \
	-projectPath /project \
	-executeMethod Fps.Editor.SmokeSuite.RunAll \
	-logFile /dev/stdout || status=$?

# コンテナの root が書いた Library などを、後の actions/cache が読み書きできるようにする
docker run --rm --volume "$PWD:/project" "$image" chown -R "$(id -u):$(id -g)" /project >/dev/null 2>&1 || true

if [ "$status" -ne 0 ]; then
	fail "SmokeSuite.RunAll exited with code $status; look for [SmokeSuite] and the FAILED run's tag in the log above"
fi

echo "nightly-smoke-ci: SmokeSuite.RunAll passed"
