#!/usr/bin/env bash
# EOS の C# SDK を dotnet/ThirdParty/EOSSDK に置く (配布の条件からリポジトリには入れない。置き場所は同じ場所の README.md)。
# 使い方: scripts/eos-sdk.sh [--from <path>]
#   --from  取り出し元。PlayEveryWare の EOS プラグインのパッケージ、Epic Developer Portal の SDK の zip か、それを展開したフォルダ
#           (既定は Library/PackageCache/com.playeveryware.eos@*。worktree ではメインのチェックアウトのものも探す)
# 置き直すたびに Source/・Bin/ と配布の条件のファイルを作り直す。取り出し元に無い OS のライブラリは置かない。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

readonly dest="dotnet/ThirdParty/EOSSDK"

usage() {
	sed -n '3,6s/^# \{0,1\}//p' "$0"
}

fail() {
	echo "eos-sdk: $*" >&2
	exit 1
}

from=""
while [ $# -gt 0 ]; do
	case "$1" in
	--from)
		[ $# -ge 2 ] && [ -n "$2" ] || fail "--from requires a value"
		from="$2"
		shift
		;;
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

if [ -z "$from" ]; then
	from="$(find_package)" || fail "no PlayEveryWare EOS package in Library/PackageCache (open the project in Unity once, or pass --from <SDK zip or folder>)"
fi
[ -e "$from" ] || fail "not found: $from"

work=""
trap '[ -z "$work" ] || rm -rf "${work:?}"' EXIT
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
