#!/usr/bin/env bash
# Unity 非依存の C# ユニットテスト (dotnet/Fps.Core.Tests) を走らせる。
# 全て通れば何も出さず、落ちたときだけビルドエラーと失敗したテスト (メッセージとスタック) を出して 1 で終わる。
# shellcheck source=scripts/_lib.sh
. "$(dirname "$0")/_lib.sh"

if ! out="$(dotnet test dotnet/Fps.slnx -v q --nologo --logger 'console;verbosity=normal' 2>&1)"; then
	# 失敗したテストのブロックは "  Failed <name>" から次のテスト結果か集計までで、ビルドエラーは 1 行ずつ出る。
	printf '%s\n' "$out" | awk '
		/^  Failed / { block = 1 }
		/^  (Passed|Skipped) |^Test Run |^[0-9]+\)/ { block = 0 }
		block && NF || / error [A-Z]+[0-9]+: / { print }
	' | problems >&2
	exit 1
fi
