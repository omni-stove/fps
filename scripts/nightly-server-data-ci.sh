#!/usr/bin/env bash
# CI (テンプレートの templates/github-workflows/map-data.yml) から、マップのデータ (server-data/maps の .fpsmap・.fpsnav・.structures.json・.ladders.json) を
# unityci/editor のコンテナの Unity で書き出し直し (make export-server-data と同じ MapCollisionExporter.Export)、
# コミットしたものと違えば失敗する。シーンを変えて書き出し忘れたまま、.NET のサーバーが古いマップで動くのに気づくため。
# 使い方: UNITY_LICENSE=<Unity_lic.ulf の中身> scripts/nightly-server-data-ci.sh
#   UNITY_IMAGE  使うイメージ (既定は ProjectSettings/ProjectVersion.txt の版の unityci/editor:ubuntu-<版>-base-3)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly maps="server-data/maps"

fail() {
	echo "nightly-server-data-ci: $*" >&2
	exit 1
}

# Export は Editor を自分で閉じないので -quit を足す。例外で失敗すれば Unity は 0 以外で終わる
status=0
bash scripts/unity-ci.sh Fps.Editor.MapCollisionExporter.Export -quit || status=$?
[ "$status" -eq 0 ] || fail "MapCollisionExporter.Export exited with code $status; look for [MapCollision] in the log above"

# 消えたマップのファイルを Export が消した分と、新しいマップの分も差分に数える
changed="$(git status --porcelain --untracked-files=all -- "$maps")"
if [ -n "$changed" ]; then
	echo "nightly-server-data-ci: the exported map data differs from what is committed:" >&2
	printf '%s\n' "$changed" >&2
	git diff --stat -- "$maps" >&2 || true
	echo "::error title=Map data out of date::$maps differs from a fresh export; run make export-server-data and commit $maps"
	exit 1
fi

echo "nightly-server-data-ci: $maps matches a fresh export"
