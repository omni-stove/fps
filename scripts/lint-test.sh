#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# lint.sh は _lib.sh でリポジトリの根に移るので、本物のリポジトリを汚さないよう使い捨ての git リポジトリに置いて走らせる。
mkdir -p "$work/scripts" "$work/Assets/game/Samples~/Demo" "$work/dotnet/Server" "$work/Other"
cp "$here/lint.sh" "$here/_lib.sh" "$work/scripts/"
cd "$work"
git init -q -b main
git config user.email test@example.com
git config user.name test

check() {
	if [ "$2" != "$3" ]; then
		printf 'FAIL %s\n--- expected\n%s\n--- actual\n%s\n' "$1" "$2" "$3"
		fail=1
	fi
}

cat >Assets/game/Ok.cs <<'EOF2'
namespace N
{
    // 順に初期化されるので先に置く。
    public class Ok
    {
        public string S = "a///b"; // 行末の // は見ない
    }
}
EOF2
git add . && git commit -qm ok

out="$(bash scripts/lint.sh --comments 2>&1)" && rc=0 || rc=$?
check "// だけなら通る" "0:" "$rc:$out"

cat >Assets/game/Samples~/Demo/A.cs <<'EOF2'
namespace N
{
    /// <summary>x</summary>
    public class A
    {
    }
}
EOF2
printf '\357\273\277/// <summary>y</summary>\nclass B {}\n' >dotnet/Server/B.cs
printf '/// <summary>z</summary>\nclass C {}\n' >Other/C.cs
git add Assets/game/Samples~/Demo/A.cs Other/C.cs
cat >Assets/game/New.cs <<'EOF2'
class New
{
        ///x
}
EOF2

out="$(bash scripts/lint.sh --comments 2>&1)" && rc=0 || rc=$?
check "/// があれば 1 で終わる" "1" "$rc"
check "/// の指摘 (追跡外・未追跡・BOM 付きも見て、Assets と dotnet の外は見ない)" "Assets/game/New.cs:3: error COMMENT: /// のコメントがある (理由だけを // で書き、/// と XML タグは使わない)
Assets/game/Samples~/Demo/A.cs:3: error COMMENT: /// のコメントがある (理由だけを // で書き、/// と XML タグは使わない)
dotnet/Server/B.cs:1: error COMMENT: /// のコメントがある (理由だけを // で書き、/// と XML タグは使わない)" "$(printf '%s\n' "$out" | LC_ALL=C sort)"

rm Assets/game/New.cs dotnet/Server/B.cs
git rm -q --cached Assets/game/Samples~/Demo/A.cs && rm Assets/game/Samples~/Demo/A.cs
out="$(bash scripts/lint.sh --comments 2>&1)" && rc=0 || rc=$?
check "消すと通る" "0:" "$rc:$out"

exit "$fail"
