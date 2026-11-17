# 専用サーバー

`dotnet/Shooter.Server` は Unity を使わない .NET のサーバーで、試合は Unity のクライアントと同じ `Shooter.Core` で動かす。
マップ・ルール・調整値は Unity で `server-data/` に書き出したものを読む。オンラインは専用サーバーにだけつなぐ (Host やプレイヤーのロビーは無い)。

各コマンドの引数と既定値は `mise tasks` を見る。

## データを書き出す

シーンやアセットを変えたら、Editor を閉じて書き出し、コミットする。

```sh
make export-server-data     # マップ → server-data/maps/
make export-server-rules    # ルールと調整値 → server-data/rules/、server-data/tuning/
```

ルールの JSON (`server-data/rules/<ルール名>.json`) は `NetMatchDefinition` の値で書き出される。
手で直してもよく、欄の意味は `NetMatchDefinition` の Tooltip、既定値と範囲は `NetMatchSettings` にある。
読めない値があると、欄の名前を添えたエラーで終了コード 3 になる。

## 動かす

```sh
make server-dotnet                                       # 手元で 0.0.0.0:7777
make eos-sdk ARGS="--fetch"                              # (任意) EOS を入れる。無ければ UDP だけのイメージになる
make server-image                                        # shooter-server:local
docker compose -f docker/server/compose.yaml up -d
```

arm64 のイメージで EOS を使うときは、Epic Developer Portal の SDK の zip から置く
(`make eos-sdk ARGS="--from <zip>"`、[dotnet/ThirdParty/EOSSDK](../dotnet/ThirdParty/EOSSDK/README.md))。

クライアントとサーバーは同じ版でビルドしたものを使う。版が違うクライアントは断られ、EOS のロビーも版ごとに分かれる。

### 止める

SIGTERM を受けると新しい人を断り、今の試合が終わるか誰もいなくなるか `-drain-timeout` (既定 600 秒) が過ぎてから止まる。
2 回送るとすぐ止まる。docker の猶予はそれより長くする (`-drain-timeout 1800` なら 1810 秒以上)。

```sh
docker stop -t 700 shooter-server
```

`compose.yaml` には 700 秒を書いてある。Kubernetes では `terminationGracePeriodSeconds` を同じように決める。

## 引数と環境変数

イメージ名より後ろの引数はサーバーに渡る。引数は `ServerLaunch` と `NetLaunchOptions`、環境変数は `ServerLaunch` と `EosCredentials`、
終了コードは `ServerProgram` と `NetV2Client` の `Exit*` にある。

EOS の認証情報はイメージに入れず `--env-file` で渡す (`eos.env` はコミットしない)。手元では `eos.local.json` からも読む。

```sh
docker run --rm --env-file eos.env -p 7777:7777/udp shooter-server:local -register-lobby 203.0.113.10:7777
```

- `-register-lobby <公開 IPv4:ポート>` を付けると、クイックマッチで探せる EOS のロビーに載る。ボイスはこのロビーの部屋を使うので、付けないサーバーにはボイスが無い。
- `--private` か `--join-code <コード>` で、参加コードの合う人だけを入れる。コードを省くと作ってログに出す。
- `-eac` は [anti-cheat.md](anti-cheat.md)。

## クライアントからつなぐ

```sh
<ゲーム> -mode client -address 127.0.0.1                         # UDP
<ゲーム> -mode client -transport eos -eos-server <サーバーの ID>     # EOS の P2P (ID は起動のログに出る)
<ゲーム> -lobby quickmatch                                       # ロビーを探す
```

マップはサーバーが決める。EOS でつなぐ開発ビルドには `-noeac` を付ける ([anti-cheat.md](anti-cheat.md))。
メニューのクイックマッチは、EOS が使えなければ `127.0.0.1:7777` へつなぐ。

## 試合のイベント

`-log-format json` を付けると、試合のイベントを 1 行 1 つの JSON で標準出力に出す。種類と項目は `ServerEventLog` にある。
普通のログの行も混ざるので、JSON の行だけを拾う。

```sh
docker logs shooter-server 2>&1 | jq -cR 'fromjson? | select(.type? == "kill")'
```

## CI のテンプレート

キットでは `templates/github-workflows/` に置き、このリポジトリでは走らせない。ゲームのリポジトリで `.github/workflows/` に写す。

- `server-image.yml`: `v*` のタグで amd64 と arm64 のイメージを作り、起動を確かめてから push する。push 先はリポジトリの変数 `SERVER_IMAGE` (既定 `ghcr.io/<owner>/<repo>/server`)。EOS は amd64 にだけ入る。
- `map-data.yml`: 毎晩マップのデータを書き出し直し、コミットしたものと違えば失敗する。Secrets の `UNITY_LICENSE` ([GameCI](https://game.ci/docs/github/activation)) が要る。失敗したら `make export-server-data` してコミットする。
