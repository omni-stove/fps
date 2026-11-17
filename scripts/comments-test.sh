#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
fail=0

# comments.sh は _lib.sh でリポジトリの根に移るので、本物のリポジトリを汚さないよう使い捨ての git リポジトリに置いて走らせる。
mkdir -p "$work/scripts"
cp "$here/comments.sh" "$here/_lib.sh" "$work/scripts/"
cd "$work"
git init -q -b main
git config user.email test@example.com
git config user.name test

cat >A.cs <<'EOF'
using System;
using System.Text;

namespace N
{
    /// <summary>説明。</summary>
    public class A
    {
        /// <summary>
        /// 値。
        /// </summary>
        public int X = 1; // 行末の説明

        // 順に初期化されるので先に置く。
        public string S = "http://x";
        public string V = @"a
// not a comment
b";
        public char C = '"'; // 引用符

        /* ブロック
           コメント */
        public int Y;
        // ReSharper disable once UnusedMember.Global
        public int Z;
    }
}
EOF
cat >B.cs <<'EOF'
class B
{
    // 消す

    int a;

    // 消す
}
EOF
printf '\357\273\277// 消す\nclass C {}\n' >C.cs
git add . && git commit -qm fixtures

check() {
	if [ "$2" != "$3" ]; then
		printf 'FAIL %s\n--- expected\n%s\n--- actual\n%s\n' "$1" "$2" "$3"
		fail=1
	fi
}

bash scripts/comments.sh extract A.cs B.cs C.cs >extracted
check "extract の塊" "A.cs:6-6 block
A.cs:9-11 block
A.cs:12-12 inline
A.cs:14-14 block
A.cs:19-19 inline
A.cs:21-22 block
B.cs:3-3 block
B.cs:7-7 block
C.cs:1-1 block" "$(sed -nE 's/^@@ (.*):[0-9a-f]+ (block|inline)$/\1 \2/p' extracted)"
check "extract の直後の行" "-- next
    public class A
    {" "$(awk '/^@@ A.cs:6-6:/ { on = 1; next } on && /^@@ / { exit } on && !/\/\/\// { print }' extracted)"

id() { sed -nE "s/^@@ ($1:[0-9a-f]+) .*/\1/p" extracted; }
cat >decisions <<EOF
$(id A.cs:6-6) delete
$(id A.cs:9-11) delete
$(id A.cs:12-12) delete
$(id A.cs:14-14) rewrite
初期化の順のため。
@@end
$(id A.cs:19-19) rewrite
// 理由
@@end
$(id A.cs:21-22) delete
$(id B.cs:3-3) delete
$(id B.cs:7-7) delete
$(id C.cs:1-1) delete
EOF
check "apply の集計" "deleted 7, rewritten 2, kept 0, errors 0" "$(bash scripts/comments.sh apply decisions)"
check "apply A.cs" "$(cat <<'EOF'
using System;
using System.Text;

namespace N
{
    public class A
    {
        public int X = 1;

        // 初期化の順のため。
        public string S = "http://x";
        public string V = @"a
// not a comment
b";
        // 理由
        public char C = '"';

        public int Y;
        // ReSharper disable once UnusedMember.Global
        public int Z;
    }
}
EOF
)" "$(cat A.cs)"
check "apply B.cs の空行" 'class B
{
    int a;
}' "$(cat B.cs)"
check "apply C.cs の BOM" "$(printf '\357\273\277class C {}')" "$(cat C.cs)"

if out="$(bash scripts/comments.sh apply decisions 2>&1)"; then
	check "書き換えた後の apply は失敗する" "exit 1" "exit 0"
fi
check "書き換えた後の apply のエラー" "9" "$(printf '%s\n' "$out" | grep -c 'differs from the extracted one')"

check "verify はコメントだけの変更を通す" "" "$(bash scripts/comments.sh verify 2>&1)"
sed -i.bak '/^using System.Text;$/d' A.cs && rm A.cs.bak
check "verify は using を消すのを通す" "" "$(bash scripts/comments.sh verify 2>&1)"
sed -i.bak 's/int a;/int b;/' B.cs && rm B.cs.bak
if out="$(bash scripts/comments.sh verify 2>&1)"; then
	check "verify はコードの変更で失敗する" "exit 1" "exit 0"
fi
check "verify の指摘" "B.cs: - int a;
B.cs: + int b;" "$out"

exit "$fail"
