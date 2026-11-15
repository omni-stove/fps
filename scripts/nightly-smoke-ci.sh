#!/usr/bin/env bash
# nightly の workflow から、SmokeSuite.RunAll を unityci/editor のコンテナの Unity で走らせ (scripts/unity-ci.sh)、その終了コードで終わる。
# game-ci/unity-builder は Unity に必ず -quit を付け、Play Mode に入る SmokeRun が走る前に Editor が閉じるので、ここで直に起動する。
# 使い方: UNITY_LICENSE=<Unity_lic.ulf の中身> scripts/nightly-smoke-ci.sh
#   UNITY_IMAGE  使うイメージ (既定は ProjectSettings/ProjectVersion.txt の版の unityci/editor:ubuntu-<版>-base-3)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

status=0
bash scripts/unity-ci.sh Fps.Editor.SmokeSuite.RunAll || status=$?

if [ "$status" -ne 0 ]; then
	echo "nightly-smoke-ci: SmokeSuite.RunAll exited with code $status; look for [SmokeSuite] and the FAILED run's tag in the log above" >&2
	exit 1
fi

echo "nightly-smoke-ci: SmokeSuite.RunAll passed"
