#!/bin/sh
# .gitattributes が拡張子ごとに付ける属性のテスト。パスは実在しなくても git check-attr が属性を返す。
set -eu
cd "$(dirname "$0")/.."
fail=0

check() {
	actual="$(git check-attr "$2" -- "$1" | sed 's/^.*: //')"
	if [ "$3" != "$actual" ]; then
		printf 'FAIL %s %s\n--- expected\n%s\n--- actual\n%s\n' "$1" "$2" "$3" "$actual"
		fail=1
	fi
}

check Assets/game/A.cs text auto
check Assets/game/A.cs eol lf
check Assets/game/Scenes/A.unity text auto
check Assets/game/Scenes/A.unity eol lf
check Assets/game/A.prefab diff unspecified
check Assets/game/Rules/NetMatch.asset text auto
check Assets/game/Scenes/NetTest/NavMesh-Floor.asset text unset
check Assets/game/Scenes/NetTest/NavMesh-Floor.asset diff unset
check Assets/game/Scenes/NetTest/NavMesh-Floor.asset merge unset
check Assets/game/Scenes/A/LightingData.asset text unset
check Assets/game/Audio/A.wav text unset
check Assets/game/Audio/A.wav diff unset
check Assets/game/A.png diff unset
check Assets/game/A.fbx diff unset
check Assets/game/A.ttf diff unset
check Assets/Plugins/A.dll diff unset
check server-data/maps/NetTest.fpsmap text unset
check server-data/maps/NetTest.fpsnav merge unset
check scripts/lint.sh eol lf

exit "$fail"
