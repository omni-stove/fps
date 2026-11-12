#!/usr/bin/env bash
# C# の書式・スタイル・アナライザを検査する。Unity 非依存のコードは dotnet/Fps.slnx で全規則を、
# Assets/game の全 .cs は書式だけを見る (MonoBehaviour などは Unity 抜きでビルドできないため)。
# あわせて Assets/ 以下の追跡ファイル・フォルダと .meta の対応を検査する。
# 問題が無ければ何も出さず、あれば指摘の行だけを出して 1 で終わる。--fix で直せるものを直す
# (.meta の抜けは直さない。GUID は Unity に生成させる)。
# shellcheck source=scripts/_lib.sh
. "$(dirname "$0")/_lib.sh"

verify=(--verify-no-changes)
[ "${1:-}" = "--fix" ] && verify=()

# Assets/ 以下で .meta が追跡されていないファイル・フォルダと、本体の無い .meta を 1 行ずつ出す。
# Unity が取り込まない名前 (. で始まる・~ で終わる) を含むパスは対象外にする。
meta_problems() {
	git -c core.quotepath=off ls-files -- Assets | awk '
		{
			n = split($0, part, "/")
			for (i = 1; i <= n; i++) if (part[i] ~ /^\./ || part[i] ~ /~$/) next
		}
		/\.meta$/ { meta[$0] = 1; next }
		{
			asset[$0] = 1
			d = $0
			while ((k = match(d, /\/[^\/]*$/)) > 0) {
				d = substr(d, 1, k - 1)
				if (d == "Assets") break
				asset[d] = 1
			}
		}
		END {
			for (a in asset) if (!((a ".meta") in meta)) print a ": error META: 対応する .meta が追跡されていない"
			for (m in meta) if (!(substr(m, 1, length(m) - 5) in asset)) print m ": error META: 本体の無い .meta が追跡されている"
		}' | LC_ALL=C sort
}

status=0
out="$( {
	dotnet format dotnet/Fps.slnx "${verify[@]}" -v q || status=1
	dotnet format whitespace Assets/game --folder "${verify[@]}" -v q || status=1
	metas="$(meta_problems)"
	[ -z "$metas" ] || { printf '%s\n' "$metas"; status=1; }
	echo "status=$status"
} 2>&1)"
status="${out##*status=}"
out="${out%status=*}"
[ -z "$out" ] || printf '%s' "$out" | problems >&2
exit "$status"
