#!/usr/bin/env bash
# C# の書式・スタイル・アナライザを検査する。Unity 非依存のコードは dotnet/Fps.slnx で全規則を、
# Assets/game の全 .cs は書式だけを見る (MonoBehaviour などは Unity 抜きでビルドできないため)。
# あわせて Assets/ 以下の追跡ファイル・フォルダと .meta の対応を検査する。
# 問題が無ければ何も出さず、あれば指摘の行だけを出して 1 で終わる。--fix で直せるものを直す
# (.meta の抜けは、.cs とフォルダなら Unity と同じ形で作る。本体の無い .meta は消さない。詳しくは fix_metas)。
# shellcheck source=scripts/_lib.sh
. "$(dirname "$0")/_lib.sh"

verify=(--verify-no-changes)
[ "${1:-}" = "--fix" ] && verify=()

# Assets/ 以下で .meta が追跡されていないファイル・フォルダと、本体の無い .meta を 1 行ずつ出す。
# --others を付けると、未追跡 (ignore されていない) のファイルも追跡済みとみなして調べる。
# Unity が取り込まない名前 (. で始まる・~ で終わる) を含むパスは対象外にする。
meta_problems() {
	git -c core.quotepath=off ls-files --cached ${1:+--others --exclude-standard} -- Assets | awk '
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

# Unity がその場で作るのと同じ中身の .meta を書く。GUID はパスから決めるので、誰がいつ作っても同じになる。
write_meta() {
	local guid
	guid="$(printf '%s' "$1" | git hash-object --stdin | cut -c1-32)"
	if [ -d "$1" ]; then
		printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$guid"
	else
		printf 'fileFormatVersion: 2\nguid: %s' "$guid"
	fi >"$1.meta"
}

# meta_problems の指摘のうち直せるものを直す。未追跡の新規ファイルも対象にする。
# .meta が無ければ、.cs とフォルダに限り作る (他の種類は取り込み設定が要るので Unity に任せる)。
# 本体が追跡済みなら .meta を git add し、未追跡なら本体と一緒に add されるよう作業ツリーに置くだけにする。
# 本体の無い .meta は改名で取り残された GUID かもしれないので、自動では消さず指摘に残す。
fix_metas() {
	local line path
	while IFS= read -r line; do
		path="${line%%: error META: *}"
		case "$line" in
		*"追跡されていない")
			if [ ! -e "$path.meta" ]; then
				[ -d "$path" ] || [ "${path%.cs}" != "$path" ] || continue
				write_meta "$path"
			fi
			if git ls-files --error-unmatch -- "$path" >/dev/null 2>&1; then git add -- "$path.meta"; fi
			;;
		esac
	done < <(meta_problems --others)
	# Unity が先に作った未追跡の .meta は --others の集合では「ある」扱いで指摘に出ないので、本体が追跡済みならここで add する。
	local meta body
	while IFS= read -r meta; do
		case "/$meta/" in */.*/ | *~/*) continue ;; esac
		body="${meta%.meta}"
		if git ls-files --error-unmatch -- "$body" >/dev/null 2>&1; then git add -- "$meta"; fi
	done < <(git -c core.quotepath=off ls-files --others --exclude-standard -- 'Assets/*.meta')
}

[ "${1:-}" = "--fix" ] && fix_metas

status=0
out="$( {
	dotnet format dotnet/Fps.slnx ${verify[@]+"${verify[@]}"} -v q || status=1
	dotnet format whitespace Assets/game --folder ${verify[@]+"${verify[@]}"} -v q || status=1
	metas="$(meta_problems)"
	[ -z "$metas" ] || { printf '%s\n' "$metas"; status=1; }
	echo "status=$status"
} 2>&1)"
status="${out##*status=}"
out="${out%status=*}"
[ -z "$out" ] || printf '%s' "$out" | problems >&2
exit "$status"
