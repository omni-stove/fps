#!/usr/bin/env bash
# C# の書式・スタイル・アナライザを検査する。Unity 非依存のコードは dotnet/Fps.slnx で全規則を、
# Assets/game の全 .cs は書式だけを見る (MonoBehaviour などは Unity 抜きでビルドできないため)。
# あわせて Assets/ 以下の追跡ファイル・フォルダと .meta の対応を検査する。
# 問題が無ければ何も出さず、あれば指摘の行だけを出して 1 で終わる。--fix で直せるものを直す
# (.meta の抜けは、作業ツリーにある .meta か、.cs とフォルダなら Unity と同じ形で作ったものを git add する)。
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

# meta_problems の指摘のうち直せるものを直す。.cs とフォルダ以外の .meta は取り込み設定が要るので Unity に任せ、
# 本体が作業ツリーにある .meta は GUID を失わないよう消さない。
fix_metas() {
	local line path ext
	local -a lines=() orphans=()
	local created=" "
	# 未追跡の新規ファイルも検査対象にするため、intent-to-add で index に載せる (内容は stage しない)。
	git ls-files --others --exclude-standard -z -- Assets | xargs -0 -r git add -N --
	while IFS= read -r line; do lines+=("$line"); done < <(meta_problems)
	for line in ${lines[@]+"${lines[@]}"}; do
		[ -n "$line" ] || continue
		path="${line%%: error META: *}"
		case "$line" in
		*"追跡されていない")
			if [ ! -e "$path.meta" ]; then
				[ -d "$path" ] || [ "${path%.cs}" != "$path" ] || continue
				write_meta "$path"
				ext="${path##*.}"
				[ -d "$path" ] && ext="/"
				created="$created$ext "
			fi
			git add -- "$path.meta"
			;;
		*"本体の無い"*) orphans+=("$path") ;;
		esac
	done
	# 改名で .meta が取り残されたときに GUID を失わないよう、同じ種類の .meta を新規に作った回は消さず指摘に残す。
	for path in ${orphans[@]+"${orphans[@]}"}; do
		[ ! -e "${path%.meta}" ] || continue
		ext="${path%.meta}"
		ext="${ext##*.}"
		case "$created" in *" $ext "*) continue ;; esac
		git rm -q -f -- "$path"
	done
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
