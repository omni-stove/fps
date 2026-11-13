#!/bin/sh
# コンテナの中で専用サーバーを起動する。待ち受けるアドレスとポートは環境変数で決め、
# docker run のイメージ名より後ろの引数 (-transport eos など) はそのまま渡す。
# -address / -port を引数でも渡すと、サーバーは「2 回指定された」として終了コード付きで終わる。
set -eu

address="${FPS_SERVER_ADDRESS:-0.0.0.0}"
port="${FPS_SERVER_PORT:-7777}"

# -logFile - でログを標準出力に出し、docker logs で読めるようにする
exec /opt/fps-server/fps-server.x86_64 -logFile - -mode server -address "$address" -port "$port" "$@"
