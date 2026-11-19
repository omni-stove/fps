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

### モードを選ぶ

モードはルールの JSON の `rules.objective` で決まり、`-rules <ルール名>` でどの JSON を読むかを選ぶ (既定は `TeamDeathmatch`)。
`server-data/rules/` には各モードの例を置いてある。

```sh
make server-dotnet ARGS="-rules FreeForAll"
```

| ルール名 | `rules.objective` | 調整値の欄 (`match.*`) | マップに要るもの |
| --- | --- | --- | --- |
| `TeamDeathmatch` | `None` | `scoreLimit`・`timeLimitSeconds`・`respawnSeconds` | チームの出現地点 |
| `Domination` | `Domination` | `dominationScoreLimit`・`captureSeconds`・`pointsPerSecond`・`maxCapturers` | 拠点 (`controlPoints`) 1 つ以上 |
| `Elimination` | `Elimination` | `roundWinsToWin`・`roundPreparationSeconds`・`roundTimeLimitSeconds` | チームの出現地点 |
| `CaptureTheFlag` | `CaptureTheFlag` | `captureTheFlag` | 両チームの旗の台 (`flagBases`) |
| `Hardpoint` | `Hardpoint` | `hardpoint` | 拠点 2 つ以上 (順に移る) |
| `SearchAndDestroy` | `SearchAndDestroy` | `searchAndDestroy` | 拠点 1 つ以上 (爆破地点に使う) |
| `FreeForAll` | `FreeForAll` | `freeForAll` | 出現地点 |
| `KillConfirmed` | `KillConfirmed` | `killConfirmed` | チームの出現地点 |
| `GunGame` | `GunGame` | `gunGame` (`tierWeaponIds` は 1 つ以上) | 出現地点 |
| `Payload` | `Payload` | `payload`・`respawnSeconds` | 台車の道筋 (`payload`) |

- マップに要るものが無いと、起動のときにマップの名前と足りないものを添えたエラー (`[Server] failed to start: ...`) で終了コード 1 になる。`mapPool` の他のマップも起動のうちに確かめる。
  例のマップ (`NetArena01`・`NetTest`) には両チームの旗の台 (`FlagBase`) を自陣の出現地点の奥に置いてあり、`make export-server-rules` で `<map>.points.json` の `flagBases` に書き出す。
  台を置く前に作ったシーンには、`make export-server-data`・`make export-server-rules` が書き出す前に台を足して保存する (台が 1 つでもあるシーンには触らない)。
  台の円盤の大きさは書き出すときの `captureTheFlag.captureRadius` に揃える。運び込んだかの判定はサーバーがルールの値で決める。
- `Payload` の道筋は、シーンの `PayloadCart` の道筋の点・中継点と、台車の範囲 (`ControlPointZone`、無ければ既定の半径 5m・高さ 6m) を
  `make export-server-rules` が `<map>.points.json` の `payload` に書き出す。攻め側はシーンの `PayloadRunner` の攻め側 (無ければ Alpha)。
  例のマップでは `NetArena01` にだけ台車を置いてあり、置く前に作ったシーンには旗の台と同じく書き出す前に足して保存する。
  道筋の無いマップ (`NetTest` など) を `-rules Payload` で選ぶと起動しないので、`make server-dotnet ARGS="-rules Payload -map NetArena01"` で立てる。
  Bot は台車のまわりを巡って押し・止め、`[Payload] cart=pushing`・`checkpoint=`・`state=Ended ... winner=` がログに出る。
- `GunGame` の段の武器 (`tierWeaponIds`) は、同じ JSON の `loadout` か `catalog` にある武器の Id にする。無い Id があると起動しない。
  近接で倒した相手の段を下げるには、`meleeWeaponIds` の先頭に近接の Id を書く。サーバーの近接はその Id で倒したことにする。
- 近接 (`rules.actions.melee`)・ストリーク (`rules.streaks`)・キルカム (`rules.deathCamera` が `KillCam`) はどのモードでも使え、
  モードの例はどれも近接とキルカムを `On` にしてある (`GunGame` はストリークを `Off`)。この 3 つの規則はスナップショットでクライアントにも届く。
- 近接・ストリークの調整値は `server-data/tuning/melee.json`・`streaks.json` から読む。クライアントの `Resources/Tuning/` と同じ中身にしておく。
  置ける物 (タレット・盾・囮) は `server-data/tuning/deployables.json` にある種類だけを置かせる。
- 文字のチャット (`rules.textChat`)・観戦 (`rules.spectating`)・置ける物 (`rules.actions.abilities`) もどのモードでも止められる。
  `Off` にすると、サーバーはチャットを誰にも配らず (`[Chat] refused ... reason=RuleOff`)、観戦の要求に映す相手を割り当てず (ラウンドの合間も)、
  置く要求を断る (`[Deploy] ... refused reason=RuleOff`)。既定は `textChat` と `spectating` が `On`、`abilities` が `Off` で、
  置ける物を使うには `deployables.json` に加えて `abilities` を `On` にする。この 3 つの規則もスナップショットでクライアントに届く。

### プレイリストで回す

`-playlist <id>` で、`server-data/playlists/*.json` (Unity の `Resources/Tuning/playlists.json` と同じ書き方) のプレイリストに沿って試合ごとにモードとマップを替える。
項目の `scene` は `server-data/maps/` のマップ名、`rules` はルール名 (省くと `TeamDeathmatch`)、`mutators` は `server-data/tuning/mutators.json` の Id にする。
`server-data/playlists/rotation.json` に TDM → Domination → Kill Confirmed の例を置いてある。

```sh
make server-dotnet ARGS="-playlist rotation"
```

- 項目がルールとマップを決めるので、`-rules`・`-map` とは一緒に渡せない (終了コード 2)。
- 最初の項目で始め、試合が終わるたびに次の項目へ進む (最後の後は最初に戻る)。`order` が `Shuffle` なら起動ごとに並びを混ぜる。
- 試合の設定 (`match.*` の点数・時間・待ち時間・`mapVoteSeconds` など) と武器は最初の項目のルールの JSON のものを使い続け、2 項目目からは `rules.*` (モードと規則) だけを替える。
- どの項目のモードも、プレイリストに出てくるすべてのマップ (と最初のルールの `mapPool`) で遊べなければならない。足りないと待ち受ける前に `[Server] failed to start: playlist <id> entry <n>/<数> cannot use rules ...` で終了コード 1 になる。
  知らないミューテーターの Id・無いルール・読めないプレイリストは終了コード 3 になる。
- `mapVoteSeconds` が 0 より大きく、マップが 2 つ以上あると、マップは投票で決め、モードとミューテーターは次の項目のものにする。投票の知らせには次の項目のルール名が載る。

### 止める

SIGTERM を受けると新しい人を断り、今の試合が終わるか誰もいなくなるか `-drain-timeout` (既定 600 秒) が過ぎてから止まる。
2 回送るとすぐ止まる。docker の猶予はそれより長くする (`-drain-timeout 1800` なら 1810 秒以上)。

```sh
docker stop -t 700 shooter-server
```

`compose.yaml` には 700 秒を書いてある。Kubernetes では `terminationGracePeriodSeconds` を同じように決める。

### キックと BAN

サーバーは標準入力から 1 行に 1 つのコマンドを読む。`make server-dotnet` ならそのまま端末に打つ。

```sh
make server-dotnet ARGS="--ban-file bans.local.json"
```

| コマンド | すること |
| --- | --- |
| `players` | つないでいる人を `player <番号> ... product_user_id=... address=...` で並べる |
| `kick <番号> [理由]` | その人を切る。また入れる |
| `ban <番号\|ProductUserId\|IP アドレス> [理由]` | 一覧に足してファイルに書き戻し、当てはまる人を切る |
| `unban <ProductUserId\|IP アドレス>` | 一覧から除いてファイルに書き戻す |
| `bans` | 一覧を並べる |
| `help` | コマンドを並べる |

- `ban <番号>` は、EOS でつないだ人なら ProductUserId を、UDP でつないだ人なら接続元の IP アドレスを載せる。
  UDP でクライアントが名乗る ProductUserId は確かめられないので、ProductUserId の BAN は EOS の通信路でだけ効く。
  EOS の P2P では接続元が分からないので、アドレスの BAN は UDP でだけ効く。同じ NAT の後ろの人もまとめて断る。
- 切られた人と断られた人のクライアントは、メニューに BAN されたこと (キックならキックされたこと) を出す。理由の文はサーバーのログと一覧にだけ残り、クライアントには送らない。
- `--ban-file` が無いと BAN は止めるまでしか覚えない。ファイルがまだ無ければ空の一覧として起動し、最初の `ban` で作る。
- 読めない一覧 (JSON の誤り・知らない欄・形の違う Id やアドレス) では、欄の場所を添えたエラーで終了コード 3 になる。
- 一覧はサーバーが書き戻すので、手で直すときはサーバーを止めてからにする。`bans.local.json` は `.gitignore` に入れてある。

```json
{
  "bans": [
    { "productUserId": "0002aaaabbbbccccddddeeeeffff0001", "reason": "aimbot", "bannedAt": "2026-11-18T09:00:00Z" },
    { "address": "203.0.113.5" }
  ]
}
```

docker では `docker run -i` (compose なら `stdin_open: true`) で標準入力を開け、`docker attach shooter-server` で打つ。
一覧はボリュームに置く (`-v ./bans:/bans` と `--ban-file /bans/bans.json`)。標準入力が閉じていると `[Admin] stdin closed` と出し、コマンドを読まずに動き続ける。

## 引数と環境変数

イメージ名より後ろの引数はサーバーに渡る。引数は `ServerLaunch` と `NetLaunchOptions`、環境変数は `ServerLaunch` と `EosCredentials`、
終了コードは `ServerProgram` と `NetV2Client` の `Exit*` にある。

EOS の認証情報はイメージに入れず `--env-file` で渡す (`eos.env` はコミットしない)。手元では `eos.local.json` からも読む。

```sh
docker run --rm --env-file eos.env -p 7777:7777/udp shooter-server:local -register-lobby 203.0.113.10:7777
```

- `-register-lobby <公開 IPv4:ポート>` を付けると、クイックマッチで探せる EOS のロビーに載る。ボイスはこのロビーの部屋を使うので、付けないサーバーにはボイスが無い。
- `--region <地域>` で、ロビーに地域 (`region` の属性) を載せる。`-register-lobby` と一緒にだけ使え、名前は小文字の英字・数字と途中の `-` で 32 文字まで (`auto` は使えない)。
  クイックマッチは設定の地域 (既定の Auto はどこでも) のサーバーを先に選び、その地域に入れるサーバーが無ければほかの地域 (地域の無いサーバーも含む) から選ぶ。
  名前はクライアントの設定の候補 (Demo では `Resources/Settings/QuickMatchRegions.json`) と揃える。
- `--private` か `--join-code <コード>` で、参加コードの合う人だけを入れる。コードを省くと作ってログに出す。
- `--ban-file <パス>` で BAN の一覧を読み書きする ([キックと BAN](#キックと-ban))。
- `--report-file`・`--stats`・`--telemetry` は [通報・統計・テレメトリ](#通報統計テレメトリ)。
- `-eac` は [anti-cheat.md](anti-cheat.md)。

## 通報・統計・テレメトリ

どれも引数で頼んだときだけ動く。頼まなければ起動のログに頼み方を 1 行ずつ出す。

| 引数 | すること |
| --- | --- |
| `--report-file <パス>` | プレイヤーの通報を 1 件 1 行の JSON で足していく。同じ人から同じ相手への通報は 300 秒に 1 件だけ残す。書けない場所なら起動で止まる (終了コード 1) |
| `--stats` | 試合の終わりにキルなどの数を EOS の Stats に送る (リーダーボードの元)。EOS の認証情報と SDK が要り、無ければ終了コード 3。失敗した送信は間を置いて送り直し、止めるときは残りを送り切るまで少し待つ |
| `--telemetry` | 運用する人の同意として、試合のまとめとクラッシュを残す。送り先は `server-data/tuning/telemetry.json` (クライアントの `Tuning/telemetry.json` と同じ形、無ければ手元のファイル) |
| `--telemetry-dir <フォルダ>` | `--telemetry` の送り先が手元のファイル (`LocalFile`) のときの置き場所。`--telemetry` と一緒にだけ使え、送り先が `LocalFile` なのに無ければ終了コード 3 |

- 統計のボードは `server-data/tuning/leaderboards.json` (クライアントの `Tuning/leaderboards.json` と同じ形、無ければ既定のボード) から読む。クライアントと同じ中身にしておく。
- 統計は EOS の ProductUserId で送るので、UDP でつないで ProductUserId を名乗らない人の数は送らない。
- docker では置き場所をボリュームにする (`-v ./records:/records` と `--report-file /records/reports.jsonl --telemetry-dir /records`)。

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
