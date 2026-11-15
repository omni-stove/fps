#!/usr/bin/env bash
# EOS の C# SDK を dotnet/ThirdParty/EOSSDK に置く (配布の条件からリポジトリには入れない。置き場所は同じ場所の README.md)。
# 使い方: scripts/eos-sdk.sh [--from <path> | --fetch]
#   --from   取り出し元。PlayEveryWare の EOS プラグインのパッケージ、Epic Developer Portal の SDK の zip か、それを展開したフォルダ
#            (既定は Library/PackageCache/com.playeveryware.eos@*。worktree ではメインのチェックアウトのものも探す)
#   --fetch  Unity を使わず、Packages/packages-lock.json に固定した版の PlayEveryWare のプラグインを git で取ってくる (git-lfs が要る)
# 置き直すたびに Source/・Bin/ と配布の条件のファイルを作り直す。取り出し元に無い OS のライブラリは置かない。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly dest="dotnet/ThirdParty/EOSSDK"

usage() {
	sed -n '3,7s/^# \{0,1\}//p' "$0"
}

fail() {
	echo "eos-sdk: $*" >&2
	exit 1
}

from=""
fetch=0
while [ $# -gt 0 ]; do
	case "$1" in
	--from)
		[ $# -ge 2 ] && [ -n "$2" ] || fail "--from requires a value"
		from="$2"
		shift
		;;
	--fetch) fetch=1 ;;
	-h | --help)
		usage
		exit 0
		;;
	*) fail "unknown argument: $1 (see --help)" ;;
	esac
	shift
done

# 新しい順に並ぶとは限らないので、更新日時の新しいものを選ぶ。
find_package() {
	local root dir
	for root in . "$(git rev-parse --path-format=absolute --git-common-dir 2>/dev/null)/.."; do
		dir="$(ls -dt "$root"/Library/PackageCache/com.playeveryware.eos@* 2>/dev/null | head -n 1)" || true
		if [ -n "$dir" ]; then
			printf '%s\n' "$dir"
			return 0
		fi
	done
	return 1
}

# サーバーのイメージはクライアントと同じ版の SDK にしたいので、Unity が使う packages-lock.json の commit を取る。
# Unity 用のライブラリが全 OS 分 LFS にあるので、C# のソースと Linux のライブラリだけを落とす
fetch_package() {
	local url hash dir lib
	url="$(sed -n '/"com.playeveryware.eos"/,/}/s/.*"version": *"\(http[^"]*\)".*/\1/p' Packages/packages-lock.json)"
	hash="$(sed -n '/"com.playeveryware.eos"/,/"hash"/s/.*"hash": *"\([0-9a-f]\{40\}\)".*/\1/p' Packages/packages-lock.json)"
	[ -n "$url" ] && [ -n "$hash" ] || fail "no git url and hash of com.playeveryware.eos in Packages/packages-lock.json"
	command -v git-lfs >/dev/null || fail "git-lfs is not installed; it is needed to fetch the EOS libraries"
	dir="$work/plugin"
	echo "eos-sdk: fetching $url at $hash" >&2
	git init -q "$dir"
	git -C "$dir" remote add origin "$url"
	GIT_LFS_SKIP_SMUDGE=1 git -C "$dir" fetch -q --depth 1 --filter=blob:none origin "$hash"
	git -C "$dir" sparse-checkout set --no-cone /Runtime/EOS_SDK/ /Runtime/Linux/
	GIT_LFS_SKIP_SMUDGE=1 git -C "$dir" checkout -q FETCH_HEAD
	# git lfs pull は木の全てのファイルを調べ、blob:none ではそのたびに 1 つずつ取りに行くので、ポインタを 1 つだけ smudge する
	lib="$dir/Runtime/Linux/libEOSSDK-Linux-Shipping.so"
	(cd "$dir" && git lfs smudge "Runtime/Linux/libEOSSDK-Linux-Shipping.so" <"$lib" >"$lib.tmp")
	mv "$lib.tmp" "$lib"
	# LFS から落とせなければ、ライブラリの代わりに数百バイトのポインタのファイルが残る
	[ "$(wc -c <"$lib")" -gt 4096 ] || fail "git lfs did not download libEOSSDK-Linux-Shipping.so"
	printf '%s\n' "$dir"
}

work=""
trap '[ -z "$work" ] || rm -rf "${work:?}"' EXIT
if [ "$fetch" -eq 1 ]; then
	[ -z "$from" ] || fail "pass either --from or --fetch, not both"
	work="$(mktemp -d)"
	from="$(fetch_package)"
elif [ -z "$from" ]; then
	from="$(find_package)" || fail "no PlayEveryWare EOS package in Library/PackageCache (open the project in Unity once, or pass --from <SDK zip or folder>)"
fi
[ -e "$from" ] || fail "not found: $from"

if [ -f "$from" ]; then
	case "$from" in
	*.zip) ;;
	*) fail "not a zip file: $from" ;;
	esac
	work="$(mktemp -d)"
	unzip -q "$from" -d "$work"
	from="$work"
fi

# 取り出し元の形ごとに、C# のソースのフォルダと、RID ごとのライブラリの場所を決める。
if [ -d "$from/Runtime/EOS_SDK" ]; then
	source_dir="$from/Runtime/EOS_SDK"
	notice_dir="$from/Runtime/EOS_SDK"
	libs=(
		"linux-x64:$from/Runtime/Linux/libEOSSDK-Linux-Shipping.so"
		"osx-arm64:$from/Runtime/macOS/libEOSSDK-Mac-Shipping.dylib"
		"osx-x64:$from/Runtime/macOS/libEOSSDK-Mac-Shipping.dylib"
		"win-x64:$from/Runtime/Windows/x64/EOSSDK-Win64-Shipping.dll"
		"win-arm64:$from/Runtime/Windows/ARM64/EOSSDK-Win64-Shippingarm64.dll"
	)
else
	sdk="$(find "$from" -maxdepth 3 -type d -path '*SDK/Source' -print -quit)"
	[ -n "$sdk" ] || fail "neither a PlayEveryWare EOS package (Runtime/EOS_SDK) nor an EOS SDK (SDK/Source): $from"
	sdk="${sdk%/Source}"
	source_dir="$sdk/Source"
	notice_dir="$sdk/.."
	libs=(
		"linux-x64:$sdk/Bin/libEOSSDK-Linux-Shipping.so"
		"linux-arm64:$sdk/Bin/libEOSSDK-LinuxArm64-Shipping.so"
		"osx-arm64:$sdk/Bin/libEOSSDK-Mac-Shipping.dylib"
		"osx-x64:$sdk/Bin/libEOSSDK-Mac-Shipping.dylib"
		"win-x64:$sdk/Bin/EOSSDK-Win64-Shipping.dll"
		"win-arm64:$sdk/Bin/EOSSDK-Win64-Shippingarm64.dll"
	)
fi

rm -rf "${dest:?}/Source" "${dest:?}/Bin" "${dest:?}/license.txt" "${dest:?}/ThirdPartySoftwareNotice.txt"
mkdir -p "$dest/Source"
count=0
while IFS= read -r -d '' file; do
	rel="${file#"$source_dir"/}"
	mkdir -p "$dest/Source/$(dirname "$rel")"
	cp "$file" "$dest/Source/$rel"
	count=$((count + 1))
done < <(find "$source_dir" -type f -name '*.cs' -print0)
[ "$count" -gt 0 ] || fail "no C# files under $source_dir"
echo "eos-sdk: $count C# files -> $dest/Source"

for entry in "${libs[@]}"; do
	rid="${entry%%:*}"
	lib="${entry#*:}"
	[ -f "$lib" ] || continue
	mkdir -p "$dest/Bin/$rid"
	cp "$lib" "$dest/Bin/$rid/"
	echo "eos-sdk: $(basename "$lib") -> $dest/Bin/$rid"
done

for notice in license.txt ThirdPartySoftwareNotice.txt; do
	[ ! -f "$notice_dir/$notice" ] || cp "$notice_dir/$notice" "$dest/"
done
