# syntax=docker/dockerfile:1
# 専用サーバー (ServerBuilder の Linux 版の出力 Builds/server-linux/) を入れるイメージ。
# コンテキストはリポジトリのルートで、入れるものは Dockerfile.dockerignore で絞る:
#   docker build --platform linux/amd64 -f docker/server/Dockerfile -t fps-server:local .
# 起動の仕方と環境変数は docs/dedicated-server.md を参照。
FROM ubuntu:24.04

# EOS の SDK は HTTPS で EOS のバックエンドに接続するので CA 証明書が要る。
# Unity のプレイヤーは PID 1 で動くとシグナルを受け取れないことがあるので tini を挟む
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates tini \
    && rm -rf /var/lib/apt/lists/*

# Unity のプレイヤーは ~/.config/unity3d に設定を書くのでホームが要る
RUN useradd --system --uid 10001 --user-group --create-home --home-dir /home/fps --shell /usr/sbin/nologin fps

WORKDIR /opt/fps-server

# サーバーの実行ファイルとデータは root の持ち物のままにし、実行するユーザーからは書き換えられないようにする
COPY Builds/server-linux/ ./
RUN chmod 0755 fps-server.x86_64
COPY --chmod=0755 docker/server/entrypoint.sh /usr/local/bin/fps-server-entrypoint

ENV FPS_SERVER_ADDRESS=0.0.0.0 \
    FPS_SERVER_PORT=7777

EXPOSE 7777/udp

USER fps:fps

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/fps-server-entrypoint"]
