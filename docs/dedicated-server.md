# 専用サーバー

`dotnet/Shooter.Server` は Unity を使わない .NET のサーバーで、試合は Unity のクライアントと同じ `Shooter.Core` で動かす。
マップ・ルール・調整値は Unity で `server-data/` に書き出したものを読む。オンラインは専用サーバーにだけつなぐ (Host やプレイヤーのロビーは無い)。

コマンドは `mise tasks`、サーバーの引数は `ServerLaunch`、ルールの欄は `NetMatchDefinition` を見る。

## データを書き出す

シーンやアセットを変えたら、Editor を閉じて書き出し、コミットする。

```sh
make export-server-data     # マップ → server-data/maps/
make export-server-rules    # ルールと調整値 → server-data/rules/、server-data/tuning/
```

`server-data/tuning/` はクライアントの `Resources/Tuning/` と同じ中身にする。`economy.json`・`mutators.json`・`playlists/*.json` は書き出されないので、手で写してコミットする。

## 動かす

```sh
make server-dotnet                                       # 手元で 0.0.0.0:7777
make eos-sdk ARGS="--fetch"                              # (任意) EOS を入れる。無ければ UDP だけのイメージになる
make server-image                                        # shooter-server:local
docker compose -f docker/server/compose.yaml up -d
```

- arm64 のイメージで EOS を使うときは、SDK の zip から置く ([dotnet/ThirdParty/EOSSDK](../dotnet/ThirdParty/EOSSDK/README.md))。
- EOS の認証情報はイメージに入れず `--env-file` で渡す (`eos.env` はコミットしない)。
- クライアントとサーバーは同じ版でビルドしたものを使う。
- 標準入力から管理のコマンド (キック・BAN) を読む。docker では `stdin_open: true` にして `docker attach` で打つ。
- 書き出すファイル (BAN の一覧・持ち物庫・通報・テレメトリ) は docker ではボリュームに置く。
- 止めるときの docker や Kubernetes の猶予は `-drain-timeout` より長くする (`compose.yaml` は 700 秒)。

## モードとマップ

- `Payload` の道筋は `NetArena01` に、`BattleRoyale`・`Extraction` のエリアと脱出地点は `NetBattleRoyale` にだけある。
- `NetBattleRoyale` の箱の表はキットの `loot.json` に無いので、ゲームが表を足すまで箱は空で開く。
- Bot は人が 1 人もいないと置かれない。試すときはクライアントを 1 人つなぐ。
- ドローン・盾・暗視とサーマルはオフラインだけで、サーバーでは効かない。盾の武器はルールと `economy.json` に入れない。
- プレイリストでは `match.*` と武器が最初の項目のルールのまま変わらないので、後の項目で使うモードの節も最初の項目に書く。

## CI のテンプレート

`templates/github-workflows/` にあり、このリポジトリでは走らせない。ゲームのリポジトリで `.github/workflows/` に写す。

- `server-image.yml`: リポジトリの変数 `SERVER_IMAGE` に push 先を入れる。
- `map-data.yml`: Secrets に `UNITY_LICENSE` を入れる。失敗したら `make export-server-data` してコミットする。
