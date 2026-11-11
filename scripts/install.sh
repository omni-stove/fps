#!/usr/bin/env bash
# dotnet/Fps.slnx の NuGet パッケージを restore する。失敗したときだけ出力する。
# shellcheck source=scripts/_lib.sh
. "$(dirname "$0")/_lib.sh"

if ! out="$(dotnet restore dotnet/Fps.slnx -v q 2>&1)"; then
	printf '%s\n' "$out" | problems >&2
	exit 1
fi
