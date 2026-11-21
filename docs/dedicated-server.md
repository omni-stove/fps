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

### 調整値 (`server-data/tuning/`)

| ファイル | 正 | 作り方 |
| --- | --- | --- |
| `ping`・`grenade`・`inventory`・`knockback`・`bots`・`assist`・`loot`・`melee`・`streaks`・`extraction`・`stash` (`.json`) | `Assets/game/Resources/Tuning/` (入口 `GameContent` の欄があればそちら) | `make export-server-rules` で写す |
| `economy.json` | `server-data` | 手で書く。オンラインのルールの武器の一覧 (`catalog`) にある武器だけを、買える品と値段として置く。財布の額は試合の設定 (`match.economy`) から読む |
| `chat-words.json` | `server-data` | 手で書く。チャットはオンラインだけなので、伏せる語はサーバーの分を置く |
| `anticheat`・`deployables`・`battle-royale` (`.json`) | `server-data` | 手で書く。クライアントに同じ名前のファイルは無い |
| `pickup-drops.json` | `server-data` | 手で書く (無ければ Core の `PickupDropSpec` の既定)。倒れた人が落とす武器・予備弾・消えるまでの秒数。オフラインではプレイヤーの `DropOnDeath` に置く値 |
| `mutators.json` | `Assets/game/Resources/Tuning/` | 使うときだけ写す。無ければオンラインの変則ルールは無し |

`server-data/tuning/` と `Resources/Tuning/` の両方にある同じ名前のファイルは、`economy.json`・`chat-words.json` を除き、中身が同じでないと `scripts/lint.sh` (CI の lint) が落ちる (`scripts/server-tuning.sh`)。サーバー用に別の中身を持たせるファイルを増やすときは、そのスクリプトの `server_owned` とこの表に足す。

`server-data/playlists/*.json` はサーバーのマップとルールの組み合わせで、クライアントの `Tuning/playlists.json` とは別に手で書く。

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
- 地面の拾い物 (`PickupSpawnPoint`) と弾薬箱 (`AmmoCrate`) は `make export-server-data` がマップごとの `<マップ>.pickups.json` に書き出す。書き出す前のマップでは拾い物が無いだけで動く。サーバーには防具が無いので、防具の拾い物は拾えない。
- Bot は人が 1 人もいないと置かれない。試すときはクライアントを 1 人つなぐ。
- ドローン・暗視とサーマルはオフラインだけで、サーバーでは効かない。
- プレイリストでは `match.*` と武器が最初の項目のルールのまま変わらないので、後の項目で使うモードの節も最初の項目に書く。
- `Infected` の調整値はルールの `match.infected` に `infected.json` と同じ項目名で書く。感染者の体力・速さ・武器 (`infectedMaxHealth`・`infectedSpeedMultiplier`・`infectedWeaponId`) はまだサーバーでは変わらない。

## CI のテンプレート

`templates/github-workflows/` にあり、このリポジトリでは走らせない。ゲームのリポジトリで `.github/workflows/` に写す。

- `server-image.yml`: リポジトリの変数 `SERVER_IMAGE` に push 先を入れる。
- `map-data.yml`: Secrets に `UNITY_LICENSE` を入れる。失敗したら `make export-server-data` してコミットする。
