#!/usr/bin/env bash
# C# の書式・スタイル・アナライザを検査する。Unity 非依存のコードは dotnet/Fps.slnx で全規則を、
# Assets/game の全 .cs は書式だけを見る (MonoBehaviour などは Unity 抜きでビルドできないため)。
# あわせて Assets/ 以下の追跡ファイル・フォルダと .meta の対応と、UI のコードに日本語の直書きが無いこと (ui_text_problems) を検査する。
# 問題が無ければ何も出さず、あれば指摘の行だけを出して 1 で終わる。--fix で直せるものを直す
# (.meta の抜けは、.cs・.asmdef・.asmref・テキストアセットとフォルダなら Unity と同じ形で作る。本体の無い .meta は消さない。詳しくは fix_metas)。
# --fix-meta は .meta の抜けだけを直して終わる。CI の lint ジョブが dotnet の復元より前に作り直すため。
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
	local head importer
	head="$(printf 'fileFormatVersion: 2\nguid: %s' "$guid")"
	if [ -d "$1" ]; then
		head="$head"$'\nfolderAsset: yes'
		importer=DefaultImporter
	else
		case "$1" in
		*.cs)
			# MonoImporter の既定値は Unity が取り込み時に足すので、GUID だけで足りる。
			printf '%s' "$head" >"$1.meta"
			return
			;;
		*.asmdef) importer=AssemblyDefinitionImporter ;;
		*.asmref) importer=AssemblyDefinitionReferenceImporter ;;
		*) importer=TextScriptImporter ;;
		esac
	fi
	printf '%s\n%s:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$head" "$importer" >"$1.meta"
}

# write_meta が Unity と同じ .meta を書ける種類か。テキストアセットは Unity が TextAsset として取り込む拡張子に限る。
# 他の種類 (画像・シーン・.uss など) は取り込み設定が要るので Unity に任せる。
meta_writable() {
	[ -d "$1" ] && return 0
	case "$1" in
	*.cs | *.asmdef | *.asmref | *.txt | *.json | *.xml | *.html | *.htm | *.bytes | *.csv | *.yaml | *.fnt) return 0 ;;
	esac
	return 1
}

# meta_problems の指摘のうち直せるものを直す。未追跡の新規ファイルも対象にする。
# .meta が無ければ、meta_writable が認める種類に限り作る。
# 本体が追跡済みなら .meta を git add し、未追跡なら本体と一緒に add されるよう作業ツリーに置くだけにする。
# 本体の無い .meta は改名で取り残された GUID かもしれないので、自動では消さず指摘に残す。
fix_metas() {
	local line path
	while IFS= read -r line; do
		path="${line%%: error META: *}"
		case "$line" in
		*"追跡されていない")
			if [ ! -e "$path.meta" ]; then
				meta_writable "$path" || continue
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

# 画面の文字は文字列表 (Assets/game/Localization/Resources/Strings と Assets/game/Resources/Localization/<言語>/) のキーで引く決まりなので、UI のコード
# (Runtime/UI と Net の HUD・ビュー) の C# の文字列リテラルと UXML の text・label・tooltip に残った日本語を 1 行ずつ出す。
# コメントの日本語は見ない。新しく足して未追跡のファイルも見る。
# 日本語は UTF-8 の先頭バイトが E3〜E9 (U+3000〜U+9FFF: 和文の句読点・かな・漢字) の文字で見分ける。
# awk は実装によって UTF-8 を文字で数えたりバイトで数えたりするので、バイトで読む perl を使う。
ui_text_problems() {
	local cs=() uxml=() path
	while IFS= read -r -d '' path; do
		[ -f "$path" ] || continue
		case "$path" in
		*.uxml) uxml+=("$path") ;;
		*) cs+=("$path") ;;
		esac
	done < <(git -c core.quotepath=off ls-files -z --cached --others --exclude-standard -- \
		':(glob)Assets/game/Scripts/Runtime/UI/**/*.cs' \
		':(glob)Assets/game/Scripts/Net/NetHud*.cs' \
		':(glob)Assets/game/Scripts/Net/*View.cs' \
		'Assets/game/Scripts/Net/NetKillFeedNames.cs' \
		'Assets/game/Scripts/Net/NetNameplates.cs' \
		'Assets/game/Scripts/Net/NetLoadoutChooser.cs' \
		':(glob)Assets/game/UI/**/*.uxml')

	if [ "${#cs[@]}" -gt 0 ]; then
		# 1 行ずつ、ブロックコメント・逐語的文字列 (@"..." は行をまたぐ) の中かを持ち越しながら読み、閉じた文字列リテラルを調べる。
		perl -ne '
			BEGIN { $block = 0; $verbatim = 0; $str = "" }
			sub check { print "$ARGV:$.: error JA: UI のコードに日本語の文字列リテラルがある (文字列表のキーにして Localizer か GameText で引く)\n" if $_[0] =~ /[\xE3-\xE9]/ }
			my ($line, $i, $instr) = ($_, 0, 0);
			my $n = length $line;
			while ($i < $n) {
				my $c = substr($line, $i, 1);
				my $two = substr($line, $i, 2);
				if ($block) {
					if ($two eq "*/") { $block = 0; $i += 2 } else { $i++ }
				} elsif ($verbatim) {
					if ($two eq "\"\"") { $str .= "\""; $i += 2 }
					elsif ($c eq "\"") { check($str); ($verbatim, $str) = (0, ""); $i++ }
					else { $str .= $c; $i++ }
				} elsif ($instr) {
					if ($c eq "\\") { $str .= $two; $i += 2 }
					elsif ($c eq "\"") { check($str); ($instr, $str) = (0, ""); $i++ }
					else { $str .= $c; $i++ }
				} elsif ($two eq "//") {
					last;
				} elsif ($two eq "/*") {
					$block = 1; $i += 2;
				} elsif ($c eq "\x27") {
					my $j = $i + 1;
					$j++ if substr($line, $j, 1) eq "\\";
					$j = index($line, "\x27", $j + 1);
					$i = $j < 0 ? $n : $j + 1;
				} elsif ($c eq "\"") {
					my $before = $i > 0 ? substr($line, $i - 1, 1) : "";
					my $before2 = $i > 1 ? substr($line, $i - 2, 1) : "";
					if ($before eq "@" || ($before eq "\$" && $before2 eq "@")) { $verbatim = 1 } else { $instr = 1 }
					$i++;
				} else {
					$i++;
				}
			}
			if ($instr) { check($str); $str = "" }
			if (eof) { check($str) if $verbatim; ($block, $verbatim, $str) = (0, 0, ""); close ARGV }
		' "${cs[@]}"
	fi

	if [ "${#uxml[@]}" -gt 0 ]; then
		perl -ne '
			print "$ARGV:$.: error JA: UXML に日本語の文字がある (text=\"#キー\" で文字列表のキーにする)\n" if /\b(?:text|label|tooltip)="[^"]*[\xE3-\xE9]/;
			close ARGV if eof;
		' "${uxml[@]}"
	fi
}

if [ "${1:-}" = "--fix-meta" ]; then
	fix_metas
	exit 0
fi
[ "${1:-}" = "--fix" ] && fix_metas

status=0
out="$( {
	dotnet format dotnet/Fps.slnx ${verify[@]+"${verify[@]}"} -v q || status=1
	dotnet format whitespace Assets/game --folder ${verify[@]+"${verify[@]}"} -v q || status=1
	metas="$(meta_problems)"
	[ -z "$metas" ] || { printf '%s\n' "$metas"; status=1; }
	texts="$(ui_text_problems)" || status=1
	[ -z "$texts" ] || { printf '%s\n' "$texts"; status=1; }
	echo "status=$status"
} 2>&1)"
status="${out##*status=}"
out="${out%status=*}"
[ -z "$out" ] || printf '%s' "$out" | problems >&2
exit "$status"
