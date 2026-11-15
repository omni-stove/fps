# syntax=docker/dockerfile:1
# Unity を使わない専用サーバー (dotnet/Fps.Server) を入れるイメージ。コンテキストはリポジトリのルートで、送るものは Dockerfile.dockerignore で絞る:
#   docker buildx build --platform linux/amd64,linux/arm64 -f docker/server-dotnet/Dockerfile -t fps-server-dotnet:local .
# EOS を使うなら先に make eos-sdk で dotnet/ThirdParty/EOSSDK に SDK を置く。起動の仕方と環境変数は docs/dedicated-server.md を参照。

# エミュレーションで SDK を動かすと何倍も遅いので、コンパイルはビルドするマシンのアーキテクチャで行い、-r で相手のアーキテクチャ向けに出す
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS build
ARG TARGETARCH
ARG FPS_BUILD_VERSION=
WORKDIR /src
# EOS の SDK のソースをアナライザの対象から外す設定がここにあり、無いと警告がエラーになってビルドが通らない
COPY .editorconfig ./
COPY dotnet/ dotnet/
COPY Assets/game/Scripts/Core/ Assets/game/Scripts/Core/
COPY Assets/game/Scripts/Protocol/ Assets/game/Scripts/Protocol/
RUN case "$TARGETARCH" in \
      amd64) rid=linux-x64 ;; \
      arm64) rid=linux-arm64 ;; \
      *) echo "unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && dotnet publish dotnet/Fps.Server/Fps.Server.csproj -c Release -r "$rid" --self-contained \
      -p:FpsBuildVersion="$FPS_BUILD_VERSION" -p:DebugType=none \
      -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -o /out
# SDK を入れたときは、その配布の条件をライブラリと一緒に配る
RUN for notice in license.txt ThirdPartySoftwareNotice.txt; do \
      if [ -f "dotnet/ThirdParty/EOSSDK/$notice" ]; then cp "dotnet/ThirdParty/EOSSDK/$notice" "/out/EOSSDK-$notice"; fi; \
    done

# self-contained なので .NET のランタイムは要らず、その依存 (libc・ICU・OpenSSL・CA 証明書) だけが入った Ubuntu 26.04 を使う
FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute

# PID 1 のプロセスにはシグナルの既定の扱いが無く、子プロセスの回収もされないので、Unity のサーバーのイメージと同じく tini を挟む
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*

# Unity のサーバーのイメージと uid を揃え、どちらのイメージに替えても同じ権限の設定 (ボリュームや securityContext) で動くようにする
RUN useradd --system --uid 10001 --user-group --create-home --home-dir /home/fps --shell /usr/sbin/nologin fps

WORKDIR /opt/fps-server

# サーバーの実行ファイルとデータは root の持ち物のままにし、実行するユーザーからは書き換えられないようにする
COPY --from=build /out/ ./
COPY server-data/ server-data/

ENV FPS_SERVER_ADDRESS=0.0.0.0 \
    FPS_SERVER_PORT=7777

EXPOSE 7777/udp

# サーバーは SIGTERM を受けると新しい参加を断り、今の試合が終わるまで (最長 -drain-timeout 秒、既定 600 秒) 止まらない。
# docker stop の猶予 (既定 10 秒) はイメージでは決められないので、止める側で
# docker stop -t 700 / docker run --stop-timeout 700 / compose の stop_grace_period: 700s を指定する (docs/dedicated-server.md)
STOPSIGNAL SIGTERM

USER fps:fps

# Unity のイメージと違って入り口のスクリプトを挟まないのは、サーバー自身が FPS_SERVER_ADDRESS / FPS_SERVER_PORT を読むため
ENTRYPOINT ["/usr/bin/tini", "--", "/opt/fps-server/fps-server"]
