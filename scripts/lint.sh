#!/usr/bin/env bash
# C# の書式・スタイル・アナライザを検査する。Unity 非依存のコードは dotnet/Fps.slnx で全規則を、
# Assets/game の全 .cs は書式だけを見る (MonoBehaviour などは Unity 抜きでビルドできないため)。
# 問題が無ければ何も出さず、あれば指摘の行だけを出して 1 で終わる。--fix で直せるものを直す。
# shellcheck source=scripts/_lib.sh
. "$(dirname "$0")/_lib.sh"

verify=(--verify-no-changes)
[ "${1:-}" = "--fix" ] && verify=()

status=0
out="$( {
	dotnet format dotnet/Fps.slnx "${verify[@]}" -v q || status=1
	dotnet format whitespace Assets/game --folder "${verify[@]}" -v q || status=1
	echo "status=$status"
} 2>&1)"
status="${out##*status=}"
out="${out%status=*}"
[ -z "$out" ] || printf '%s' "$out" | problems >&2
exit "$status"
