# 専用サーバーの Docker イメージ

`ServerBuilder` の Linux 版の出力 (`Builds/server-linux/`) を `docker/server/Dockerfile` でイメージにする。
コンテナはユーザー `fps` (uid 10001) で動き、`7777/udp` で待ち受ける。

## イメージを作る

Unity の Editor を閉じてから、リポジトリのルートで次を実行する。Unity Hub で Linux Build Support と
Dedicated Server Build Support (Linux) のモジュールを入れておくこと。

```sh
make server-image                                  # Builds/server-linux/ を作り直し、fps-server:local を作る
make server-image ARGS="--tag fps-server:dev"      # タグを変える
make server-image ARGS="--skip-build"              # 今ある Builds/server-linux/ をそのまま使う
```

イメージは `linux/amd64` で作る (サーバーは x86_64 の Linux 向けにしかビルドしないため)。
Apple Silicon の Mac でも Docker Desktop のエミュレーションで動く。

## 起動する

```sh
docker run --rm --name fps-server -p 7777:7777/udp fps-server:local
```

ログは標準出力に出る (`docker logs -f fps-server`)。待ち受けを始めると次の行が出る。

```
[Net] server listening on 0.0.0.0:7777
```

止めるときは `docker stop fps-server` (SIGTERM でサーバーが終了する)。

### 手元のクライアントからつなぐ

UDP の専用サーバーにつなぐクライアントは、通常のプレイヤーのビルド (`make build`) を `-mode client` で起動する。

```sh
Builds/fps.app/Contents/MacOS/fps -mode client -address 127.0.0.1
```

ポートを変えたときは `-port` も合わせる。別のマシンのサーバーには `-address` にそのマシンの IPv4 アドレスを渡す。

## 環境変数

| 名前 | 既定 | 内容 |
| --- | --- | --- |
| `FPS_SERVER_PORT` | `7777` | 待ち受ける UDP のポート。変えたら `-p` の両側も同じ番号にする |
| `FPS_SERVER_ADDRESS` | `0.0.0.0` | 待ち受ける IPv4 アドレス。コンテナの外からつなぐには `0.0.0.0` のままにする |
| `EOS_PRODUCT_ID` | なし | EOS の認証情報。`-transport eos` で起動するときだけ要る |
| `EOS_SANDBOX_ID` | なし | 同上 |
| `EOS_DEPLOYMENT_ID` | なし | 同上 |
| `EOS_CLIENT_ID` | なし | 同上 |
| `EOS_CLIENT_SECRET` | なし | 同上 |

待ち受けのアドレスとポートは環境変数で渡す。`-address` / `-port` をイメージ名の後ろに書くと 2 回指定したことになり、
サーバーは `[Net] invalid arguments: ... is given more than once` を出して終わる。

```sh
docker run --rm -e FPS_SERVER_PORT=9000 -p 9000:9000/udp fps-server:local
```

### EOS で待ち受ける

イメージ名より後ろの引数はそのままサーバーに渡る。EOS の認証情報はイメージに入れず、実行時に環境変数で渡す
(コマンドラインやシェルの履歴に残さないよう `--env-file` を使う)。`eos.env` はコミットしないこと。

```sh
# eos.env の中身は 1 行に 1 つ、EOS_PRODUCT_ID=... の形
docker run --rm --env-file eos.env -p 7777:7777/udp fps-server:local -transport eos
```

認証情報が欠けていると `[Net] EOS credentials missing, using UDP` と足りない変数の名前を出し、UDP で待ち受ける。
EOS のサーバーでは `[Net] EOS server ready id=<サーバーの ID>` が出るので、クライアントは
`-mode client -transport eos -eos-server <サーバーの ID> -noeac` でつなぐ (EAC については [anti-cheat.md](anti-cheat.md))。

## CI でのリリース

`v*` のタグを push すると `.github/workflows/server-image.yml` が専用サーバーをビルドし、
`ghcr.io/<owner>/<repo>/server` に push する。タグはタグ名そのもの (`v1.2.3`) と、semver なら `1.2.3` と `1.2` も付く。

```sh
docker pull ghcr.io/<owner>/<repo>/server:v1.2.3
```

Unity のビルドには [GameCI](https://game.ci/docs/github/activation) の手順で作ったリポジトリの Secrets
`UNITY_LICENSE`、`UNITY_EMAIL`、`UNITY_PASSWORD` が要る。
