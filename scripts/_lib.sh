# shellcheck shell=bash
# lint.sh / test.sh が共有する前処理。成功時は何も出さず、問題の行だけを出すための関数を置く。
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# 絶対パスと行末の [project] を落として相対パスにし、同じ指摘が 2 経路から出たときに 1 行にまとめる。
problems() {
	sed -E "s#$PWD/##g; s# \[[^]]*\]\$##" | awk '!seen[$0]++'
}
