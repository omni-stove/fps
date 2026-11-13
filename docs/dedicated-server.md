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
make server-image ARGS="--version v0.2.0"          # サーバーに入れる版を決める
```

### 版

ビルドの版 (実行時の `Application.version`) は `--version` で渡したもの、無ければ `git describe --tags --always --dirty`
の出力になる。先頭の `v` は外すので、`v0.2.0` のタグを付けたコミットなら `0.2.0`、タグの無いコミットなら短い sha
(`abc1234`)、コミットしていない変更があれば `-dirty` が付く。版は英数字と `. - _` で 32 文字以内でなければならない。
`make build` / `make build-server` でも同じで、`make build-server linux ARGS="--version v0.2.0"` のように渡す。

版はビルドの間だけ `ProjectSettings/ProjectSettings.asset` の `bundleVersion` に入れ、ビルドが終われば (失敗しても)
元に戻すので、ビルドのあとに `ProjectSettings` の差分は残らない。クライアントもサーバーも起動するとまず次の行を出す。

```
[Version] 0.2.0
```

EOS のロビーは版ごとに分かれる (`fps:<版>` のバケツ) ので、クライアントとサーバーは同じ版でビルドしたものを使う。
クライアントは接続の承認データに自分の版を載せ、サーバー (Host を含む) は版が違うクライアントを承認で断る。
サーバーには `[Net] rejected client <Id>: version mismatch (client 0.1.0, server 0.2.0)` が出て、クライアントは
「版が違います (サーバー 0.2.0 / このゲーム 0.1.0)。」を出してメインメニューに戻る。版を載せない古いビルドのクライアントも
(`unreadable connection payload` として) 断られ、「サーバーに接続を断られました。」と出る。

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
サーバーは止まる前につないでいるクライアントへ止まると知らせて切り (`[Net] server stopping; told <人数> client(s) and disconnected them`)、
クライアントは「サーバーが止まりました。」を出してメインメニューに戻る。知らせが届かずに切れたとき (サーバーが落ちた・回線が切れた) は、
クライアントは Netcode が切断に気づいたところで「サーバーからの応答が途絶えました (タイムアウト)。」を出して戻る。
クライアントのログには `[Net] disconnected from server id=<Id> kind=ServerStopped connected=True reason="fps:server-stopped"` のように理由が出る。

### 手元のクライアントからつなぐ

UDP の専用サーバーにつなぐクライアントは、通常のプレイヤーのビルド (`make build`) を `-mode client` で起動する。

```sh
Builds/client-macos/fps.app/Contents/MacOS/fps -mode client -address 127.0.0.1
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

### EOS のロビーに登録する

UDP で待ち受けるサーバーに `-register-lobby <公開アドレス:ポート>` を付けると、EOS に Device ID でログインし、
クライアントがサーバーを探せるロビーを作る。公開アドレスはクライアントから届く IPv4 アドレスとポート
(`docker run -p` で公開した側の番号) で、`0.0.0.0` やホスト名は使えない。EOS の認証情報が無いと起動に失敗する。

```sh
docker run --rm --env-file eos.env -p 7777:7777/udp fps-server:local -register-lobby 203.0.113.10:7777
```

ロビーは `fps-server:<版>` のバケツに、最大 8 人で作り、次の公開の属性を付ける。`-lobby join` の Host のロビー
(`fps:<版>`) とはバケツを分けている。

| 属性 | 値 |
| --- | --- |
| `server` | `-register-lobby` に渡したアドレスとポート |
| `mode` | `tdm` |
| `version` | ビルドの版 |
| `players` | 接続しているプレイヤーの人数 (整数) |

ログには次のように出る。

```
[Lobby] created id=<ロビー Id> max=8 owner=<サーバーの ID> server=203.0.113.10:7777 mode=tdm version=0.2.0 players=0
[Lobby] registered id=<ロビー Id> server=203.0.113.10:7777 players=0
[Lobby] players 0 -> 1
[Lobby] updated id=<ロビー Id> players=1
```

ロビーやログインを失うと (`[Lobby] lost the server lobby ...`)、5 秒おきに作り直す。起動してから 60 秒たっても
一度も登録できなければ、認証情報や設定の誤りとみなして終了コード 7 で終わる。一度登録できた後は試合を止めず、
60 秒ごとにエラーを出しながら作り直しを続ける。止めるとロビーを閉じ (`[Lobby] closed id=<ロビー Id>`)、
閉じ終えるまで最長 3 秒終了を待つ。

### クライアントからクイックマッチでつなぐ

クライアントはメインメニューの「ONLINE - QUICK MATCH」か、起動引数 `-lobby quickmatch` で、
登録されたサーバーを探して UDP でつなぐ。クライアントにも EOS の認証情報が要る (検索に EOS へログインするため)。

```sh
./fps -lobby quickmatch
```

`fps-server:<自分の版>` のバケツを探し、`version` が自分の版と同じで、`players` がロビーの最大人数より少なく、
`server` が IPv4:ポートのロビーのうち、`players` がいちばん多いもの (同じならロビー ID の順で先のもの) を選ぶ。
クライアントはロビーには入らず、`server` のアドレスへそのままつなぐ。

```
[Lobby] found 2 lobby(s) in fps-server:0.2.0
[Lobby] quick match picked id=<ロビー Id> server=203.0.113.10:7777 players=1/8; starting client 203.0.113.10:7777
```

つなげるサーバーが無ければ 5 秒おきに探し直し (`[Lobby] no server to join among 1 found (full x1); searching again in 5s`)、
30 秒たっても見つからなければ `[Lobby] no quick match server found after 30s (...)` を出して終わる
(メニューから来たときはこの理由を添えてメニューへ戻り、batchmode では終了コード 7)。
`-lobby quickmatch` は `-transport udp` だけを受け付け、`-lobby-id` / `-lobby-max-players` / `-mode` / `-address` / `-port` とは併用できない。

## CI でのリリース

`v*` のタグを push すると `.github/workflows/server-image.yml` が専用サーバーをビルドし、
`ghcr.io/<owner>/<repo>/server` に push する。タグはタグ名そのもの (`v1.2.3`)、サーバーが `[Version]` で出す版
(`1.2.3`、`v1-rc` なら `1-rc`) と、semver なら `1.2` も付く。版はタグ名から `-buildVersion` で渡し、git describe は使わない。
版にできないタグ名 (`+` を含むものなど) ではビルドの前に失敗する。

```sh
docker pull ghcr.io/<owner>/<repo>/server:v1.2.3
```

Unity のビルドには [GameCI](https://game.ci/docs/github/activation) の手順で作ったリポジトリの Secrets
`UNITY_LICENSE`、`UNITY_EMAIL`、`UNITY_PASSWORD` が要る。
