#!/usr/bin/env bash
# <base> から今の作業ツリーまでに変わったファイル (未追跡の新規ファイルを含む) から、CI の lint と test のどこまでを回すかを決める。
#   scripts/affected.sh <base>       full= test= format= を true か false で 1 行ずつ出す ($GITHUB_OUTPUT にそのまま足せる形)
#   scripts/affected.sh --cs <base>  変わって今もある Assets/game と dotnet の .cs を 1 行ずつ出す (lint.sh --since が書式を見る範囲)
# full: 全部の検査の前提 (ビルド設定・書式の規則・CI とそのスクリプト) が変わったか、<base> を引けないとき。
# test: dotnet のテストに影響しないと確かなファイル (no_test_effect) の外が変わったとき。full なら true。
# format: .cs が変わったとき。full なら true。
# shellcheck source=scripts/_lib.sh
. "$(dirname "$0")/_lib.sh"

mode=flags
if [ "${1:-}" = "--cs" ]; then
	mode=cs
	shift
fi
base="${1:?usage: scripts/affected.sh [--cs] <base>}"

# dotnet の外から取り込むフォルダは csproj の Compile Include="../../<dir>/**" から引き、プロジェクトを足しても書き直さずに済むようにする。
compile_roots() {
	echo dotnet/
	local csproj
	while IFS= read -r csproj; do
		sed -nE 's#.*Include="\.\./\.\./([^"*]*)\*\*.*#\1#p' "$csproj"
	done < <(git ls-files -- 'dotnet/*.csproj')
}

# テストはリポジトリのあちこちの JSON・.asset・dll を読むので、読むものを並べるのではなく、読まないと確かなものだけを並べる。
# ビルドに入らない .cs (と、その .cs.meta) と、文書・CI の設定・スクリプト。
no_test_effect() {
	local root
	case "$1" in
	*.md | docs/* | .github/* | scripts/*) return 0 ;;
	*.cs | *.cs.meta)
		while IFS= read -r root; do
			case "$1" in "$root"*) return 1 ;; esac
		done <<<"$roots"
		return 0
		;;
	esac
	return 1
}

is_full() {
	case "$1" in
	.editorconfig | */.editorconfig | global.json | */global.json | Directory.Build.* | */Directory.Build.* | \
		dotnet/*.props | dotnet/*.slnx | dotnet/*.csproj | mise.toml | .mise/* | .github/workflows/ci.yml | \
		scripts/_lib.sh | scripts/affected.sh | scripts/install.sh | scripts/lint.sh | scripts/test.sh)
		return 0
		;;
	esac
	return 1
}

# 消したファイルもビルドを変えるので、改名は消した側と足した側の 2 つのパスとして数える。
if ! changed="$(git -c core.quotepath=off diff --name-only --no-renames "$base" -- 2>/dev/null)"; then
	if [ "$mode" = flags ]; then
		printf 'full=true\ntest=true\nformat=true\n'
	fi
	exit 0
fi
changed="$(
	printf '%s\n' "$changed"
	git -c core.quotepath=off ls-files --others --exclude-standard
)"

if [ "$mode" = cs ]; then
	printf '%s\n' "$changed" | while IFS= read -r path; do
		case "$path" in
		Assets/game/*.cs | dotnet/*.cs)
			if [ -f "$path" ]; then printf "%s\n" "$path"; fi
			;;
		esac
	done
	exit 0
fi

roots="$(compile_roots)"
full=false test=false format=false
while IFS= read -r path; do
	[ -n "$path" ] || continue
	if is_full "$path"; then full=true; fi
	case "$path" in *.cs) format=true ;; esac
	if ! no_test_effect "$path"; then test=true; fi
done <<<"$changed"

if [ "$full" = true ]; then
	test=true format=true
fi
printf 'full=%s\ntest=%s\nformat=%s\n' "$full" "$test" "$format"
