# 専用サーバー

専用サーバーは Unity を使わない .NET のプログラム (`dotnet/Shooter.Server`) で、試合のルールや当たりの判定は Unity のクライアントと
同じ `Shooter.Core` のコードで行う。マップ・ルール・調整値は Unity の Editor で `server-data/` に書き出したファイルから読む。
イメージは `docker/server/Dockerfile` で作り、コンテナはユーザー `shooter` (uid 10001) で動いて `7777/udp` で待ち受ける。
クライアントはゲームの通常のビルド (`make build`) で、`Shooter.Protocol` の通信手順でつなぐ (下の「クライアントからつなぐ」)。

Unity の Editor の中でサーバーを兼ねる遊び方 (Host・`-mode host`) と、プレイヤーが Host になるロビー (`-lobby create|join`、
メニューの「ロビーを作る／入る」) は無い。オンラインでは専用サーバーにつなぐ。

## サーバーのデータを書き出す

サーバーはシーンとアセットを読めないので、Unity の Editor (閉じておく) で書き出してコミットする。

```sh
make export-server-data     # 各マップのコリジョン (.fpsmap)・NavMesh (.fpsnav)・壊せる建物・はしご → server-data/maps/
make export-server-rules    # 試合の設定とルール → server-data/rules/、出現地点と拠点 → server-data/maps/<マップ>.points.json、
                            # 印・グレネード・持ち物・吹き飛ばし・ボットの調整値 → server-data/tuning/
```

- 書き出すマップは `-map` で選べるマップ (`BuildSceneList.ServerScenes`、既定は `NetTest` と `NetArena01`)。
  無いシーンは `NetSceneBuilder`・`ArenaSceneBuilder` で作ってから書き出す。
- 試合の設定 (点数・時間・リスポーン・体力・拠点制圧の値・マップの候補と投票の秒数・人数の差の上限・非公開と参加コード・チャットの上限・
  ロードアウトと選べる武器・移動の値と体の寸法) は
  `NetMatchDefinition` のアセット (サンプルでは `Rules/NetMatch.asset`。無ければ `NetSceneBuilder` が既定の値で作る) に置き、
  入口 (`GameContent`) のルールと、`NetMatchDefinition` の Rules に並べたルール (サンプルでは `TeamDeathmatch`・`Domination`) ごとに
  `server-data/rules/<ルール名>.json` にする。ロードアウトと選べる武器は、クライアントと同じく入口のものを先に使い、無ければ `NetMatchDefinition` のもの。
- 調整値 (`ping.json`・`grenade.json`・`inventory.json`・`knockback.json`・`bots.json`・`assist.json`) は、オフラインと同じく入口の欄の JSON、
  無ければ `Resources/Tuning/<名前>.json` を写す。どちらも無ければ項目を省いた `{}` (既定値) を書く。
  クライアントは承認のときにサーバーからこの値を受け取るので、ゲームが値を変えたら書き出し直してサーバーを作り直せばよい。
- 出現地点はシーンの `NetSpawnPoint`、拠点は `ControlPointZone`、壊せる建物は `DestructibleStructure`、はしごは `SceneLadder` から読む。

## 手元で動かす

```sh
make server-dotnet                                   # 0.0.0.0:7777 で待ち受ける
SHOOTER_SERVER_PORT=9000 make server-dotnet              # ポートを変える
dotnet run --project dotnet/Shooter.Server/Shooter.Server.csproj -- -mode server -rules Domination -map NetArena01   # 拠点制圧で動かす
```

## イメージを作る

`dotnet/Shooter.Server` を self-contained で `dotnet publish` したものと `server-data/`、EOS の C# SDK のライブラリ
(`dotnet/ThirdParty/EOSSDK` に置いてあれば) を入れる。Unity も Editor を閉じることも要らない。

```sh
make eos-sdk ARGS="--fetch"                            # (任意) EOS の SDK を Unity を使わずに置く。無ければ EOS 無しのイメージになる
make server-image                                      # shooter-server:local を作る (Docker が動いているマシンのアーキテクチャ)
make server-image ARGS="--platform linux/arm64"        # arm64 のイメージを作る
make server-image ARGS="--tag shooter-server:dev --version v0.2.0"
```

`make eos-sdk ARGS="--fetch"` は `Packages/packages-lock.json` に固定した PlayEveryWare のプラグインから SDK を取る
(git-lfs が要る)。プラグインには Linux の arm64 のライブラリが無いので、arm64 のイメージで EOS を使うには
Epic Developer Portal の SDK の zip から置く (`make eos-sdk ARGS="--from <zip>"`、[dotnet/ThirdParty/EOSSDK](../dotnet/ThirdParty/EOSSDK/README.md))。
ライブラリの無いイメージは `[Eos] sdk=included library=libEOSSDK-Linux-Shipping.so (missing; ...)` を出し、UDP でだけ待ち受けられる。

### 版

版は `--version` で渡したもの、無ければ `git describe --tags --always --dirty` の出力で、ビルドに焼き込む。
先頭の `v` は外すので、`v0.2.0` のタグを付けたコミットなら `0.2.0`、タグの無いコミットなら短い sha (`abc1234`)、
コミットしていない変更があれば `-dirty` が付く。版は英数字と `. - _` で 32 文字以内でなければならない。
起動時に `-buildVersion` を付ければそちらが勝つ。クライアントの版は `make build` の `--version` で同じように決まる。
クライアントもサーバーも起動するとまず次の行を出す。

```
[Version] 0.2.0
```

EOS のロビーは版ごとに分かれる (`shooter-server:<版>` のバケツ) ので、クライアントとサーバーは同じ版でビルドしたものを使う。
クライアントは承認のデータに自分の版を載せ、サーバーは版が違うクライアントを断る。
サーバーには `[Net] rejected client <Id>: version mismatch (client 0.1.0, server 0.2.0)` が出て、クライアントは
「版が違います (サーバー 0.2.0 / このゲーム 0.1.0)。」を出してメインメニューに戻る。
通信の手順の版 (`MatchProtocol.Version`) が違うクライアントも `shooter:rejected:protocol-mismatch` で断る。

## 起動する

```sh
docker run --rm --name shooter-server -p 7777:7777/udp shooter-server:local
docker compose -f docker/server/compose.yaml up -d     # stop_grace_period: 700s を書いてある
```

ログは標準出力に出る (`docker logs -f shooter-server`)。待ち受けを始めると次の行が出る。

```
[Net] server listening on 0.0.0.0:7777
```

止めるときは `docker stop -t 700 shooter-server`。サーバーは SIGTERM を受けてもすぐには止まらず、今の試合が終わるのを待つ
(次の「試合が終わってから止める」)。`-t` を付けないと docker は 10 秒で SIGKILL を送るので、試合の途中で落ちる。
サーバーは止まる前につないでいるクライアントへ止まると知らせて切り、クライアントは「サーバーが止まりました。」を出してメインメニューに戻る。
知らせが届かずに切れたとき (サーバーが落ちた・回線が切れた) は、クライアントは切断に気づいたところで
「サーバーからの応答が途絶えました (タイムアウト)。」を出して戻る。

### 試合が終わってから止める

サーバーは終了を求められると (SIGTERM と SIGINT。`docker stop`、`docker kill -s TERM`、`kill <PID>`、Ctrl+C)、「止まる準備中」になる。

1. EOS のロビーに登録していれば (`-register-lobby`)、すぐにロビーを閉じ、クイックマッチで選ばれないようにする。
2. 新しく来たクライアントを承認で断る。サーバーには `[Net] rejected client <Id>: the server is draining before it stops` が出て、
   クライアントは「サーバーが止まる準備中のため参加できません。別のサーバーにつないでください。」を出してメインメニューに戻る。
3. 今の試合が終わるか、つないでいるクライアントがいなくなるか、`-drain-timeout <秒>` が過ぎたところで、
   残っているクライアントへ止まると知らせて終了コード 0 で止まる。

`-drain-timeout` を付けなければ 600 秒待つ。`-drain-timeout 0` なら待たずに止まる (上限は 3600 秒)。
止まる準備中にもう一度 SIGTERM を受けると、試合を待たずにすぐ止まる。つないでいるクライアントがいない、または試合が終わって
次の試合を待っているときは、最初の SIGTERM ですぐ止まる。ログには次のように出る。

```
[Net] stop requested; draining: refusing new players and waiting up to 600s for the match to end (players=1). Send the stop signal again to stop now
[Lobby] unregistering id=<ロビー Id>
[Net] draining: waiting for the match to end (players=1, 570s left)
[Net] drain finished: the match ended; stopping with exit code 0
```

docker が SIGKILL を送るまでの猶予は、待つ秒数とロビーを閉じる時間 (最長 3 秒) より長くする。猶予はイメージでは決められないので、
止める側で指定する。

```sh
docker stop -t 700 shooter-server                                   # 止めるたびに指定する
docker run --rm --stop-timeout 700 -p 7777:7777/udp shooter-server:local   # コンテナを作るときに決めておく
docker compose -f docker/server/compose.yaml stop                # stop_grace_period: 700s を書いてある
docker kill -s TERM shooter-server; docker kill -s TERM shooter-server  # 2 回送るとすぐ止まる
```

`-drain-timeout` を変えたら猶予もそれに合わせる (例: `-drain-timeout 1800` なら `-t 1810` 以上)。
Kubernetes では `terminationGracePeriodSeconds` を同じように決める。

## 引数と環境変数

| 引数 | 内容 |
| --- | --- |
| `-map <マップ名>` | 最初の試合のマップ。`server-data/maps/<マップ名>.*` を読む。無ければルールの `mapPool` の先頭、それも空なら `NetTest` |
| `-rules <ルール名>` | 試合のルール。`server-data/rules/<ルール名>.json` を読む。無ければ `TeamDeathmatch` |
| `-bots <人数>` | 人と Bot を合わせてこの人数になるまで Bot を足す |
| `-transport udp\|eos` | 待ち受ける通信路。既定は `udp` |
| `-register-lobby <公開アドレス:ポート>` | EOS のロビーに載せる (下の「EOS のロビーに登録する」) |
| `--private` | 参加コードを知っている人だけが入れる非公開の試合にする (下の「非公開の試合」) |
| `--join-code <コード>` | 非公開の試合の参加コード (英数字と `-` `_` で 16 文字まで。小文字は大文字にそろえる)。付けると `--private` も付けたことになる |
| `-drain-timeout <秒>` | 止める前に試合の終わりを待つ秒数 |
| `-log-format text\|json` | `json` なら試合のイベントを JSON で出す |
| `-buildVersion <版>` | 版を焼き込んだものから変える |

| 環境変数 | 既定 | 内容 |
| --- | --- | --- |
| `SHOOTER_SERVER_PORT` | `7777` | 待ち受ける UDP のポート。変えたら `-p` の両側も同じ番号にする |
| `SHOOTER_SERVER_ADDRESS` | `0.0.0.0` | 待ち受ける IPv4 アドレス。コンテナの外からつなぐには `0.0.0.0` のままにする |
| `EOS_PRODUCT_ID` | なし | EOS の認証情報。`-transport eos` と `-register-lobby` で使う |
| `EOS_SANDBOX_ID` | なし | 同上 |
| `EOS_DEPLOYMENT_ID` | なし | 同上 |
| `EOS_CLIENT_ID` | なし | 同上 |
| `EOS_CLIENT_SECRET` | なし | 同上 |

待ち受けのアドレスとポートは環境変数で渡す。`-address` / `-port` と環境変数の両方で渡すと
`[Net] invalid arguments: -port is given more than once (SHOOTER_SERVER_PORT is set too) ...` を出して終了コード 2 で終わる。
`-net-latency` / `-net-loss` / `-net-jitter` は使えず、付けると同じく終了コード 2 で終わる。

```sh
docker run --rm -e SHOOTER_SERVER_PORT=9000 -p 9000:9000/udp shooter-server:local
```

### 非公開の試合

`--private` を付けると、参加コードの合う人だけを入れる。コードを省くと 6 文字のコードを作り、起動のログに出す。

```
[Server] private match join_code=7KQ2MX (generated; pass --join-code to choose one)
```

ルールの JSON (`server-data/rules/<ルール名>.json`) の `match` の `privateMatch` と `joinCode` でも同じ設定ができる。
起動の引数があればそちらを使う (`--join-code` は JSON の `joinCode` より優先し、`--private` だけなら JSON の `joinCode` を使う)。
引数でも JSON でも非公開でなければ `[Server] public match` を出す。
コードの合わない人・コードを送らなかった人は、版を比べた後に `[Net] rejected client <番号>: wrong join code for the private match`
(送らなかったときは `no join code for the private match`) で断る。送られたコードはログに出さない。
`-register-lobby` と一緒に使うと、ロビーに `private` と `join_code` の属性が付く。
コードは小文字を大文字にそろえて比べるので、`abc234` と `ABC234` は同じコードとして通る。

```sh
docker run --rm -p 7777:7777/udp shooter-server:local --private                       # コードを作らせる
docker run --rm -p 7777:7777/udp shooter-server:local --join-code ROOM-42             # コードを決める (非公開にもなる)
docker run --rm --env-file eos.env -p 7777:7777/udp shooter-server:local \
  --private -register-lobby 203.0.113.10:7777                                          # コードで探せるロビーに載せる
```

### EOS で待ち受ける

イメージ名より後ろの引数はそのままサーバーに渡る。EOS の認証情報はイメージに入れず、実行時に環境変数で渡す
(コマンドラインやシェルの履歴に残さないよう `--env-file` を使う)。`eos.env` はコミットしないこと。
手元では作業ディレクトリの `eos.local.json` からも読む。

```sh
# eos.env の中身は 1 行に 1 つ、EOS_PRODUCT_ID=... の形
docker run --rm --env-file eos.env -p 7777:7777/udp shooter-server:local -transport eos
```

`-transport eos` は EOS の P2P (ソケット名 `shooter`) で待ち受け、Device ID でログインできたら
`[Net] EOS server ready id=<サーバーの ID> ...` を出す。クライアントは `-mode client -transport eos -eos-server <サーバーの ID>` でつなぐ
(EAC については [anti-cheat.md](anti-cheat.md))。
認証情報が欠けていると `[Net] EOS credentials missing, using UDP` と足りない変数の名前を出し、UDP で待ち受ける。
EOS の SDK かそのライブラリが無ければ理由を出して終了コード 3、ログインできなければ `[Net] failed to start the server: ...` を出して
終了コード 4 で終わる。EOS でつないだ人は、名乗った鍵ではなく EOS が確かめた ProductUserId で見分け (つなぎ直しの成績の引き継ぎも)、
JSON のイベントに `productUserId` を付ける。

### EOS のロビーに登録する

`-register-lobby <公開アドレス:ポート>` を付けると、EOS に Device ID でログインし、クライアントのクイックマッチがサーバーを探せるロビーを作る。
公開アドレスはクライアントから届く IPv4 アドレスとポート (`docker run -p` で公開した側の番号) で、`0.0.0.0` やホスト名は使えない。
EOS の SDK か認証情報が無いと、理由を出して終了コード 3 で終わる。
ロビーに載せられるのは UDP で待ち受けるサーバーだけで、`-transport eos` と一緒には使えない。

```sh
docker run --rm --env-file eos.env -p 7777:7777/udp shooter-server:local -register-lobby 203.0.113.10:7777
```

ロビーは `shooter-server:<版>` のバケツに作り、次の公開の属性を付ける。ロビーのオーナーはサーバーが
Device ID でログインしたユーザー。ロビーでは RTC 部屋を開くので、ID で入ったクライアントのメンバーはそこでボイスを使える。
メンバーの上限は、試合の人数 (ルールの `match.maxPlayers`、既定は 8 人) にサーバー自身の 1 人を足した数 (EOS の上限の 64 人まで)。

ボイスの部屋はこのロビーのものなので、`-register-lobby` を付けないサーバー (`-transport eos` だけのものも) にはボイスが無い。
テキストのチャットはロビーを使わず試合の通信で送るので、ロビーが無くても使える。
敵の声はサーバーではなく、受ける側のクライアントが切る。

| 属性 | 値 |
| --- | --- |
| `server` | `-register-lobby` に渡したアドレスとポート |
| `mode` | `tdm` |
| `version` | ビルドの版 |
| `players` | 接続しているプレイヤーの人数 (整数。Bot は数えない) |
| `voice_enabled` | `1` (RTC 部屋を開いている) |
| `private` | 非公開の試合のときだけ `1`。クイックマッチはこのロビーを選ばない |
| `join_code` | 非公開の試合のときだけ、その参加コード |
| `skill_average` | 接続しているプレイヤーのうち、接続のときに実力の値 (`SkillRating`) を送った人の平均 (整数。端数は四捨五入)。値を送った人が一度も入っていなければ付けない。EOS の属性は消せないので、値を送った人が全員抜けても最後の平均を残す |

ログには次のように出る。人が入る・出るたびに、変わった属性だけを更新する。

```
[Lobby] created id=<ロビー Id> max=9 owner=<サーバーの ID> rtc=on server=203.0.113.10:7777 mode=tdm version=0.2.0 players=0 voice_enabled=1
[Lobby] registered id=<ロビー Id> server=203.0.113.10:7777 players=0
[Lobby] players 0 -> 1
[Lobby] skill average - -> 1200
[Lobby] updated id=<ロビー Id> players=1 skill_average=1200
```

ロビーやログインを失うと (`[Lobby] lost the server lobby ...`)、5 秒おきに作り直す。起動してから 60 秒たっても
一度も登録できなければ、認証情報や設定の誤りとみなして終了コード 7 で終わる。一度登録できた後は試合を止めず、
60 秒ごとにエラーを出しながら作り直しを続ける。止めるとロビーを閉じ (`[Lobby] closed id=<ロビー Id>`)、
閉じ終えるまで最長 3 秒終了を待つ。

## クライアントからつなぐ

クライアントはゲームの通常のビルド (`make build`) で、つなぎ方は起動引数で決まる。

```sh
<ビルドしたゲーム> -mode client -address 127.0.0.1                 # 起動してすぐ UDP でつなぐ (2 つ起動すると互いが見える)
<ビルドしたゲーム> -mode client -transport eos -eos-server <サーバーの ID>   # EOS の P2P でつなぐ
<ビルドしたゲーム> -lobby quickmatch                               # 起動してすぐロビーを探してつなぐ
<ビルドしたゲーム>                                                 # メニューの「ONLINE - QUICK MATCH」でつなぐ
```

- `-address`・`-port` (既定 `127.0.0.1:7777`) は UDP のサーバー。マップは `-map` ではなく、承認のときにサーバーが知らせるマップのシーンへ移る
  (クライアントには `-map` を付けられない)。そのシーンがビルドに無い・`.fpsmap` が無ければ理由を出してそのシーンに留まる。
- 起動引数でつなぐときもメニューは出ていて、承認されたところでマップのシーンへ移る。切断してメニューへ戻った後は、メニューのクイックマッチでつなぎ直せる。
- `-mode server` は受け付けない (`make server-dotnet` かサーバーのイメージを使う)。`-mode host`・`-lobby create|join` は無い。
  `-net-latency` / `-net-loss` / `-net-jitter` もクライアントでは使えない。誤った引数では `[Net] invalid arguments: ...` を出して終了コード 2 で終わる。
- エディタの外では EAC が既定で有効なので、EAC を通さずに EOS でつなぐなら `-noeac` を付ける ([anti-cheat.md](anti-cheat.md))。

### クイックマッチ

メインメニューの「ONLINE - QUICK MATCH」と起動引数の `-lobby quickmatch` は、`-register-lobby` で載ったサーバーを探してつなぐ。

- メニューのクイックマッチは、起動引数に `-address`・`-port`・`-eos-server` があればロビーを探さずにそのサーバーへつなぐ。
- 探すには EOS のプラグイン (`Shooter.NetEos`) と認証情報 (環境変数か `eos.local.json`) が要る。メニューのクイックマッチで、
  EOS がビルドに無いか認証情報が揃っていない UDP のときは、`[Lobby] the EOS lobby is not available ...; quick match connects to 127.0.0.1:7777`
  を出して手元のサーバー (`make server-dotnet`) へつなぐ。`-lobby quickmatch` と EOS のクイックマッチは、探せなければ理由を出して止まる。
- `shooter-server:<自分の版>` のバケツを探し、`version` が自分の版と同じで、`players` がロビーの最大人数より少なく、
  `server` が IPv4:ポートのロビーのうち、`players` がいちばん多いもの (同じならロビー ID の順で先のもの) を選ぶ。
  自分の実力の値 (パーティならメンバーの平均) を渡して探したときは、`skill_average` と自分の値の差が小さいものを先に選び、
  差が同じものの中で今の順 (人数の多い順、次にロビー ID の順) にする。`skill_average` の無いロビーは差がいちばん大きいとみなす。
  このときのログは `[Lobby] logging in to search shooter-server:0.2.0 near skill 1000`、
  `[Lobby] quick match picked id=<ロビー Id> server=203.0.113.10:7777 players=1/8 skill=1050 (self 1000); connecting ...` になる。
  クライアントはロビーには入らず、`server` のアドレスへ UDP でつなぐ。クイックマッチは UDP だけで、`-lobby quickmatch -transport eos` は理由を出して終了コード 2 で断られる
  (EOS でつなぐなら `-mode client -transport eos -eos-server <サーバーの ID>`)。

```
[Lobby] logging in to search shooter-server:0.2.0
[Lobby] logged in with the device id user=<自分の ID>
[Lobby] found 2 lobby(s) in shooter-server:0.2.0
[Lobby] quick match picked id=<ロビー Id> server=203.0.113.10:7777 players=1/8; connecting client 203.0.113.10:7777
```

つなげるサーバーが無ければ 5 秒おきに探し直し (`[Lobby] no server to join among 1 found (full x1); searching again in 5s`)、
30 秒たっても見つからなければ `[Lobby] no quick match server found after 30s (...)` を出して終わる
(メニューから来たときはこの理由を添えてメニューへ戻り、batchmode では終了コード 7)。
`-lobby quickmatch` は `-mode` / `-address` / `-port` / `-eos-server` / `-register-lobby` とは併用できない。

### 試合の中のクライアント

- 自分の移動はサーバーと同じ Core の移動と、マップの `.fpsmap`・`.ladders.json` で予測して入力を送り、スナップショットの処理済みの入力の tick で照合する。
  Editor では `server-data/maps/` を直接読み、ビルドには `MapCollisionBuildFiles` が `StreamingAssets/maps/` として入れる。
  移動の値 (移動・体の寸法・伏せ) は、サーバーが承認のときに送るルールのファイルの `motor` の値を使う (`[Net] motor from the server's rules ...`)。
  食い違って巻き戻したときは `[Net] reconciled tick=... error=...` を出す。他のプレイヤーは 2 tick 遅らせて補間する。
  他のプレイヤーの見た目は `Resources/NetV2/RemotePlayerBody` のプレハブで差し替えられ、無ければ体の寸法のカプセルを出す (しゃがみ・伏せは縦に縮める)。
- 伏せ (Prone) とはしごは、試合のルールの `prone`・`ladder` が On のときだけ使える。はしごにつかまると視点をはしごへ向ける (`[Ladder] grab ...`)。
- 射撃は今の武器の射撃モードと連射間隔 (入口の `GameContent` のロードアウトと武器の一覧) で送り、弾倉が空なら空撃ちの音を出してリロードを頼む。
  リロード・グレネード・印は押したときに要求を送る。キル・被弾・ヒット・爆発はオフラインと同じ HUD とエフェクト (`CombatEvents`) に流し、`[Combat]` の行を出す。
- 印は `PingController` に、拠点制圧の拠点はマップの `ControlPointZone` の円盤と HUD の拠点の欄に出す。
- 壊れる建物は、サーバーが送る壊れた部品 (`StructureStateMessage`) をシーンの見た目と予測の当たりに写す。
- 保存してあるロードアウトを承認されたときに頼み、倒れている間はロードアウトを選ぶ画面を出す (受け入れられた並びだけを保存する)。
- サーバーとの接続を失うと、理由 (「サーバーが止まりました。」・タイムアウト・断られた・版が違う) を出してメニューへ戻る。
  batchmode では終了コード (切断 0・つながらない 5・ロビーで見つからない 7) で終わる。

## NetArena01 で拠点制圧 (Domination) を遊ぶ

マップは `-map`、ルールは `-rules` で選ぶ。マップ NetArena01 には拠点 A (Alpha の陣地寄り)・B (中央)・C (Bravo の陣地寄り) が
置いてあり、チームデスマッチのときは使われない。

```sh
docker run --rm --name shooter-server -p 7777:7777/udp shooter-server:local -map NetArena01 -rules Domination
```

試合が始まると、サーバーのログに拠点の数と、占領・中立化のたびに次の行が出る。

```
[Domination] net points=3 scoreLimit=... captureSeconds=... pointsPerSecond=...
[Domination] captured point=A team=Alpha
[Domination] neutralized point=A from=Alpha
```

2 つのクライアントを `-mode client -address <サーバーのアドレス>` でつなぐと、それぞれ別のチームに入る。HUD に A・B・C の
3 つの拠点が並び、拠点の床の円と HUD の文字が持ち主のチームの色で塗られる。円の中に立つと占領が進み、両チームがいると止まる。

## ルールのファイルの対戦の周りの欄

`server-data/rules/<ルール名>.json` の `match` には、点数や時間のほかに次の欄がある。`make export-server-rules` が
`NetMatchDefinition` の値で書き出し、手で直してもよい (直したらイメージを作り直す)。欄を省くと既定の値になり、
範囲の外の値や知らない欄があると、`[Server] cannot read rules <ルール名>: ...` に欄の名前 (`match.mapVoteSeconds` など) を添えて出し、
終了コード 3 で終わる。

| 欄 | 既定 | 範囲 | 内容 |
| --- | --- | --- | --- |
| `mapPool` | `[]` | マップの Id を 16 個まで (重複・`/` は不可) | 次の試合で替えられるマップ (下の「次の試合のマップの候補を置く」) |
| `mapVoteSeconds` | `0` | 0 以上 | 試合の合間のマップの投票の秒数。`0` なら投票しない |
| `maxTeamSizeDifference` | `0` | 0 以上の整数 | 次の試合を始めるときに許す 2 チームの人数の差。`0` なら人数をそろえない (下の「チームの人数をそろえる」) |
| `privateMatch` | `false` | `true` / `false` | 参加コードの合う人だけを入れる (上の「非公開の試合」) |
| `joinCode` | `""` | 英数字と `-` `_` で 16 文字まで | 非公開の試合の参加コード。非公開で空なら起動のときに作る。`--join-code` があればそちらを使う |
| `chatIntervalSeconds` | `1` | 0 以上 | 1 人がチャットを送れる間隔の秒数 (下の「チャットの上限」) |
| `maxChatLength` | `120` | 1〜170 | チャットの 1 行の文字数の上限 |

書き出したままのファイルはどれも既定の値で、投票・人数の調整はせず、公開の試合になる。

```json
"maxPlayers": 8,
"mapPool": [],
"mapVoteSeconds": 0,
"maxTeamSizeDifference": 0,
"privateMatch": false,
"joinCode": "",
"chatIntervalSeconds": 1,
"maxChatLength": 120
```

## 次の試合のマップの候補を置く

ルールのファイル (`server-data/rules/<ルール名>.json`) の `match.mapPool` に、次の試合で替えられるマップの Id (シーン名) を並べる。

```json
"mapPool": ["NetTest", "NetArena01"],
```

起動するとき、サーバーは最初のマップ (`-map`、無ければ候補の先頭) と候補のマップをすべて読み、マップごとの `[Server] map ...` の行に続けて次の行を出す。

```
[Server] map NetTest colliders=... from=...
[Server] map NetArena01 colliders=... from=...
[Server] map pool loaded 2 maps: NetTest, NetArena01
```

- 候補のマップが 1 つでも読めなければ `[Server] map pool: map <Id> has no collision: ...` のように理由を出し、終了コード 3 で終わる。
- 候補のマップに出現地点 (`.points.json`) が無い・どちらかのチームの出現地点が無い・拠点制圧のルールで拠点が無いときも、
  試合の合間に止まらないよう、起動のうちに `[Server] failed to start: map <Id> has no spawn points: ...` や
  `[Server] failed to start: cannot change to map <Id> ...` を出して終了コード 1 で終わる。
- 候補が空 (`[]`) なら今までどおり 1 つのマップだけを読み、`map pool` の行は出さない。

マップを替えるのは、試合が終わって次の試合を始めるときだけ。替えると `[Match] map changed from=<前の Id> to=<次の Id>` を出し、
(`-log-format json` なら JSON の `map_changed` も出す) 当たり判定・出現地点・拠点・はしご・壊せる建物・乗り物・Bot の NavMesh を
新しいマップのものにしてから、全員を新しいマップの出現地点へ置き直す。壊れた建物は、マップを替えると壊れていない状態に戻る。
次の試合のマップを選ぶ仕組み (投票) が選ばなければ、同じマップのまま続ける。
途中から入る人には承認のメッセージで今のマップを知らせる。

### 試合の合間のマップの投票

ルールのファイルの `match.mapVoteSeconds` を 0 より長くし、読んだマップが 2 つ以上あると、試合が終わるたびに投票をする。
読んだマップが 1 つだけか `mapVoteSeconds` が 0 (既定) なら投票はせず、投票のメッセージも送らない。

```json
"mapPool": ["NetTest", "NetArena01", "NetArena02"],
"mapVoteSeconds": 15,
```

- 今のマップを除いた候補から最大 3 つを出す。候補が並ぶ順は `mapPool` の順。
- 試合が終わると投票を始めて全員に `MapVoteStateMessage` を送り、票が変わったときと 1 秒ごと、途中から入った人にも送る。
- 締め切ると一番票の多いマップに決め、並んだときと誰も入れなかったときは、並んだ候補から試合の Id で決まる乱数で選ぶ。
  決まったマップを載せた `MapVoteStateMessage` を送り、次の試合をそのマップで始める。
- 次の試合を始めるまでの秒 (`restartDelaySeconds`) が `mapVoteSeconds` より短いときは、`mapVoteSeconds` まで待つ。
- 抜けた人の票は締め切る前なら消す。締め切った後の票と、候補に無い番号の票は受け付けない。

ログには次の行が出る (`-log-format json` なら票ごとに `map_vote`、替えたときに `map_changed` の JSON も出る)。

```
[MapVote] opened current=NetTest candidates=[NetArena01, NetArena02] seconds=15.0
[MapVote] cast player=1 map=NetArena02 result=Cast
[MapVote] decided map=NetArena02 reason=Majority votes=1
[Match] map changed from=NetTest to=NetArena02
```

ネット対戦のクライアント (`NetV2Client`) は、決まったマップを覚えておき、スナップショットの試合の状態が `Ended` から抜けた (次の試合が始まった) ときに
`[MapVote] net next match map NetTest -> NetArena02` を出して、つないだままそのマップのシーンを読み込み直す。
シーンがビルドに無ければ、最初につないだときと同じくエラーにして抜ける。

## チームの人数をそろえる

入ってきた人は、今までどおり人数の少ないチームへ入る。接続のときにパーティの Id (`ConnectionPayload.PartyId`) を送った人は、
同じ Id の仲間が多くいるチームへ入る。そのチームの人 (Bot は数えない) がすでに `maxPlayers` の半分 (端数は切り上げ) いれば、
人数の少ないチームへ入る。
切断してつなぎ直した人は、パーティより前のチームに戻る。

ルールのファイルの `match.maxTeamSizeDifference` を 1 以上にすると、次の試合を始めるときに 2 チームの人数の差がそれを超えていれば、
多いチームの最近入った人 (Bot も含む) から少ないチームへ移す。

```json
"maxTeamSizeDifference": 1,
```

- 同じパーティの人はまとめて移すか、移さない。移すと差が縮まらない組は移さない。
- 試合の途中では移さない。`0` (既定) なら移さない。
- 移した人ごとに次の行を出す (`-log-format json` なら `team_balanced` も出す)。

```
[Match] balanced player=3 from=Alpha to=Bravo
```

## チャットの上限

チャットは全体 (`All`) とチーム (`Team`) の 2 つで、チームの文はサーバーが送った人の今のチームの人にだけ配る。
ルールのファイルの `match.chatIntervalSeconds` (1 人が送れる間隔の秒数) と `match.maxChatLength` (1 行の文字数) で上限を決める。

```json
"chatIntervalSeconds": 1,
"maxChatLength": 120
```

- 本文は前後の空白を落とし、改行を空白 1 つにしてから数える。空の文は `Empty`、長すぎる文は `TooLong`、
  前に受け付けた文から `chatIntervalSeconds` たっていない文は `TooSoon` で断り、断った文は誰にも配らない (送った人にも返さない)。
- `maxChatLength` は 170 まで (通信の 1 つのメッセージの上限 512 バイトに、UTF-8 で 3 バイトの文字が収まる数)。
- 本文の差し替えや伏せ字はしない (するならゲームの側で)。ログにも本文は出さず、長さだけを出す。

試合を用意するたびに上限を、受け付けるたびと断るたび (1 秒に 1 行まで) に次の行を出す (`-log-format json` なら `chat` と `chat_refused` も出す)。

```
[Chat] gate interval=1.00 maxLength=120
[Chat] accepted sender=1 channel=Team length=5 at=12.50
[Chat] refused sender=1 reason=TooSoon
```

## 設定を試す

マップの投票と人数の調整を試すには、ルールのファイル (例: `server-data/rules/TeamDeathmatch.json`) の `match` を次のように直して起動する。

```json
"mapPool": ["NetTest", "NetArena01"],
"mapVoteSeconds": 15,
"maxTeamSizeDifference": 1,
```

```sh
dotnet run --project dotnet/Shooter.Server/Shooter.Server.csproj -- -mode server -rules TeamDeathmatch -map NetTest
```

1. 起動のログに `[Server] map pool loaded 2 maps: NetTest, NetArena01` と `[Server] public match` が出る。
2. クライアント (`-mode client -address 127.0.0.1`) を 3 つつなぐと、Alpha に 2 人・Bravo に 1 人入る。
3. 試合が終わると `[MapVote] opened current=NetTest candidates=[NetArena01] seconds=15.0` が出る。
   15 秒後 (または `restartDelaySeconds` の長いほう) に `[MapVote] decided map=NetArena01 ...` と `[Match] map changed from=NetTest to=NetArena01` が出る。
4. 差が 1 のままなら誰も移さない。投票の間に Bravo の 1 人を切ると差が 2 になり、次の試合を始めるときに
   Alpha で後から入った人について `[Match] balanced player=.. from=Alpha to=Bravo` が出る。

非公開を試すには、同じサーバーを `--join-code TEST-1` を付けて起動し直す。

1. 起動のログに `[Server] private match join_code=TEST-1` が出る。
2. コードを送らないクライアント (`-mode client -address 127.0.0.1`) は、サーバーに `[Net] rejected client <番号>: no join code for the private match` が出て断られ、
   メインメニューに戻る。

## サーバーがすること

- 起動時に `server-data/maps/<マップ名>.fpsmap` と `.fpsnav` を読む。作業ディレクトリの `server-data/`、
  無ければ実行ファイルの隣の `server-data/` を探す。無いか読めなければ探した場所を出して終了コード 3 で終わる。
- はしごの形と調整値は `server-data/maps/<マップ名>.ladders.json` から読む。サーバーとクライアントの予測はどちらもこの並びの番号で
  はしごを指すので、同じファイルを読む。ファイルが無いマップははしごが無いものとして動く。起動時の `[Server] map ...` の `ladders=` が読んだ本数。
  伏せの寸法と速さはルールのファイルの `motor` の `proneHeight`・`proneSpeed`・`diveDistance`・`diveSeconds` (無ければ既定値) で決まる。
  リーン (`leanSideOffset`・`leanAngleDegrees`・`leanSpeed`)・空中のジャンプ (`airJumps`・`airJumpSpeed`)・水泳 (`swimSpeed`・`swimBuoyancy`・`swimBreathSeconds`)・
  壁走り (`wallRunEnabled`・`wallRunMaxSeconds`・`wallRunMinSpeed`・`wallRunGravityScale`・`wallRunJumpOffSpeed`) も同じ `motor` から読み、
  承認のメッセージでクライアントの予測に渡す。水泳と壁走りの値は `make export-server-rules` が `Resources/Tuning/traversal.json` (入口の欄が先) から書き出す。
  つかまる・離れる・伏せる・起き上がる・飛び込むたびに `[Net] ladder owner=.. tick=.. climbing=..`・`[Net] prone ... prone=..`・`[Net] dive ... diving=..` を出す。
- 水の場所は `server-data/maps/<マップ名>.water.json` (`make export-server-data` がシーンの有効な `Water` の軸にそろった箱と水面の高さを書き出す) から読む。
  ファイルが無いマップは水が無いものとして動く。起動時の `[Server] map ...` の `water=` が読んだ数。次の試合でマップを替えると、新しいマップの水で泳ぐ。
  クライアントの予測も同じファイルを読む (`[Net] map collision ...` の `water=`)。
  入力の `LeanLeft`・`LeanRight` のビットで傾け (両方なら傾けない)、スナップショットの人の欄に傾き・泳いでいるか・壁走りの側・使った空中のジャンプの回数を載せる。
  クライアントは自分の予測をこれらと照らし合わせ、水面・息・壁の向きなど欄に無いものは同じ tick の予測と水から埋める。
  他の人の体は、オフラインと同じく傾き (`leanAngleDegrees`)・泳ぎ・壁走りの側で足元を軸に傾けて見せる。
  傾く向きが替わる・泳ぎ始める/やめる・潜る/顔を出す・壁を走り始める/離れる・空中で跳ぶたびに
  `[Net] lean owner=.. tick=.. side=left|right|none`・`[Net] swim ... swimming=..`・`[Net] swim ... underwater=..`・`[Net] wallrun ... running=.. side=..`・`[Net] airjump ... used=..` を出す。
  潜ったまま息 (`swimBreathSeconds`) が切れると `[Net] swim ... outOfBreath=..` を出し、撃った人のいないダメージとしてルールに通して `[Net] drown owner=.. tick=.. damage=.. outcome=..` を出す。
  ダメージの量と間隔はルールのファイルにまだ無いので、`SwimTuning` の既定値 (10 を 1 秒ごと) になる。
- 乗り物は `server-data/maps/<マップ名>.vehicles.json` (`make export-server-data` がシーンの有効な `Vehicle` から書き出す) から読む。
  サーバーとクライアントはどちらもこの並びの番号で乗り物を指す。ファイルが無いマップは乗り物が無いものとして動く (`[Server] map ...` の `vehicles=`)。
  乗り物の当たり判定は `.fpsmap` に入れず、ファイルの車体の箱で、撃った人が見ていた tick まで巻き戻して当てる。
  座席はサーバーが決める (距離・空き・壊れていないか)。運転席のクライアントが自分の物理で動かした姿を送り、サーバーは `drive` の速さの上限とマップの物で確かめ、
  外れていれば `[Vehicle] '<id>#<番号>' corrected driver=.. reason=..` を出して姿を直す。乗り降り・傷み・爆発は `[Vehicle]` で始まる行に出す。
- ルールを読むと `[Server] rules <名前> objective=...` を出す。マップの出現地点と拠点は `server-data/maps/<マップ名>.points.json`、
  印の調整値は `server-data/tuning/ping.json` (無ければ既定値) から読む。拠点制圧のルールでは `[Domination] net points=3 ...` を出す。
  ルールが無いか読めなければ終了コード 3、マップに出現地点のファイルが無ければ終了コード 1 で終わる。
- 射撃は撃った人が見ていた tick まで他の人を巻き戻して (12 tick まで) 当て、壁は `.fpsmap` で取る。
  巻き戻すのは位置・しゃがみ・リーンの傾きで、傾けた人の体は足元を軸に、目がリーンのずれ (`MotorSpec.LeanSideOffset`) の所へ来るまで回す。
  ログは `[Net] hit ...`・`[Net] shot ...`・`[Net] rejected shot ...`・`[Grenade] net ...`。
  グレネードの調整値は `server-data/tuning/grenade.json` (無ければ既定値) から読む。
- 爆発はダメージを与えた生きている相手を、オフラインと同じ計算 (`KnockbackSpec`) で吹き飛ばす。強さ・重さ (人は `playerWeight`、Bot は `enemyWeight`)・
  空中の重力と効きは `server-data/tuning/knockback.json` (無ければ既定値) から読み、承認 (`ConnectAccepted`) でクライアントの予測にも渡す。
  飛んでいる状態は持ち主のスナップショットで送り、予測は飛ばされた tick から入力をやり直す。ログは `[Net] knockback owner=.. launched|landed ..`。
- 持ち物 (アイテムの種類・マス目や重さの決め方・出現のたびに持つアイテム) は `server-data/tuning/inventory.json`
  (無ければアイテムなし) から読み、出現のたびに持たせ直す。持ち主の置き直す・使う・捨てるの頼みを確かめ、中身が変わった tick に
  持ち主にだけ送る。決め方の `countWeightInCarryLoad` が true なら、持ち物の重さを移動の遅さに数える。
  地面の拾い物はまだ無いので、捨てたアイテムは消える。ログは `[Inventory] items=..`・`[Inventory] move|use|drop owner=..`。
  `weaponId` を持つアイテムは武器で、出現で持たせた武器が持ち物のマスを占め、重さは武器の `weight` を使う
  (持ち物の重さを移動の遅さに数えるときは、武器の分は移動のルールが武器として数えるので除く)。使うとその武器に持ち替え、捨てられない。
  武器のアイテムに `ammoItemId` があれば、その武器の予備弾は持ち物の弾薬アイテムの数になり、リロードで減る。出現のたびに、持たせた武器の
  最初の予備弾の合計まで弾薬を足す。ログは `[Inventory] equip owner=..`・`[Inventory] restock owner=..`。
  `attachment` を持つアイテムは武器に付ける物で、武器のアイテムの `attachmentSlots` にあるその取り付け口 (`slot`) に付く (`weaponIds` があればその武器だけ)。
  持ち物から構えている武器に付けると持ち物のマスから消え、重さは武器の重さに足し、`attachment` の倍率と弾倉の増減を武器の値に掛ける。
  同じ取り付け口の物は持ち物に戻し、外した物も持ち物に戻す (入らなければ付け替え・取り外しを断る)。弾倉が減って入らない弾は持ち物の弾薬に戻す。
  付けた物は持ち物の状態の武器の山に載せて持ち主に送り、出現で武器を持たせ直すと外れる。ログは `[Inventory] attach|detach owner=..`・`[Net] attachments player=..`。
- 壊せる建物の部品の並びは `server-data/maps/<マップ名>.structures.json` から読む。
  無いマップでは `[Destruction] map <マップ名> has N destructible structure(s) but no ...` を出し、部品は壊れない壁として残る。
  破片が当たるダメージ (DebrisCrush) はまだ無い。
- Bot は、人と Bot を合わせて `-bots` とルールの `minPlayers` の大きいほうの人数になるまで足し、
  人が入るとその人のチームの Bot を抜く。人が 1 人もいなければ Bot も置かない。Bot は `.fpsnav` の経路をたどり、人と同じ入力で
  移動と射撃の検査を通る。はしごのリンクでは、はしごへ進んでつかまり、上り下りして渡る (ルールの `actions.ladder` が `Off` なら、
  はしごを通る経路は使わない)。書き出した NavMesh は壊せる建物をくり抜いていないので、壊れていない部品を通り抜ける経路は使わず、
  壊れたら通る。見える距離・反応・照準の誤差・経路を引き直す間隔などは `server-data/tuning/bots.json` (無ければ既定値) から読む。
  ログは `[Bot] fill target=..`・`[Bot] added player=.. name=[BOT] B1 team=..`・`[Bot] removed player=.. reason=..`、
  2 秒ごとの `[Bot] pos player=.. x=.. y=.. z=.. state=.. health=..`、はしごの `[Bot] ladder player=.. ladder=.. climb=Up|Down|Done`、
  進めなくなったときの `[Bot] stuck player=..`。倒した・倒されたは人と同じ `[Match] kill ...` と JSON の `kill` の行に出る。
  Bot は JSON の `player_joined` / `player_left` にも出るが、drain が待つ人数とロビーに載せる人数には数えない。
- 待ち受けられなければ (ポートが使われているなど) `[Net] failed to start the server: ...` を出して終了コード 4 で終わる。
- 承認では版と鍵を確かめ、断ったときは `[Net] rejected client <Id>: ...` を出して理由をクライアントへ送る。
  つないでから 10 秒以内に承認のデータを送らない接続を切り、同じ鍵のクライアントがつなぎ直してきたら前の接続を切る。

## 試合のイベントを JSON で出す

`-log-format json` を付けると、今までのログ (`[Net]` / `[Match]` 付きの行) に加えて、サーバーの起動・終了と試合のイベントを
1 イベント 1 行の JSON で標準出力に出す。付けないとき (`-log-format text`、既定) はこれまでのログのままで、JSON の行は出ない。

```sh
docker run --rm --name shooter-server -p 7777:7777/udp shooter-server:local -log-format json
```

同じ標準出力に今までのログの行も混ざるので、jq では各行を文字列として読み (`-R`)、JSON として読めて `type` のある行だけを拾う。

```sh
docker logs shooter-server 2>&1 | jq -cR 'fromjson? | select(.type?)'
docker logs shooter-server 2>&1 | jq -cR 'fromjson? | select(.type? == "kill")'
```

```
{"type":"server_started","time":"2026-11-13T09:00:00.000Z","version":"0.2.0","transport":"udp","endpoint":"0.0.0.0:7777"}
{"type":"player_joined","time":"2026-11-13T09:00:12.345Z","matchId":"6c1f3b2a9d0e4f5a8b7c6d5e4f3a2b1c","playerId":"1","team":"Alpha","rejoined":"false"}
{"type":"player_joined","time":"2026-11-13T09:00:15.002Z","matchId":"6c1f3b2a9d0e4f5a8b7c6d5e4f3a2b1c","playerId":"2","team":"Bravo","rejoined":"false"}
{"type":"match_started","time":"2026-11-13T09:00:15.003Z","matchId":"6c1f3b2a9d0e4f5a8b7c6d5e4f3a2b1c","players":"2"}
{"type":"kill","time":"2026-11-13T09:00:41.870Z","matchId":"6c1f3b2a9d0e4f5a8b7c6d5e4f3a2b1c","killerId":"1","victimId":"2","teamKill":"false"}
{"type":"match_ended","time":"2026-11-13T09:05:15.010Z","matchId":"6c1f3b2a9d0e4f5a8b7c6d5e4f3a2b1c","winner":"Alpha","scoreAlpha":"30","scoreBravo":"12"}
```

### 項目

どの行も `type` と `time` から始まる。値は数も真偽値もすべて文字列 (`"3"`、`"true"`) で、無い値は項目ごと出さない。
人の名前 (表示名) は出さない。

| 項目 | 内容 |
| --- | --- |
| `type` | イベントの種類 (下の表) |
| `time` | サーバーで起きた時刻。UTC の ISO 8601 でミリ秒まで (`2026-11-13T09:00:12.345Z`) |
| `matchId` | 試合の Id (32 桁の 16 進数)。サーバーが試合を用意したときと、試合が終わって次の試合を始めるときに作り直す。`server_*` には無い |
| `playerId` / `killerId` / `victimId` | プレイヤーの Id (サーバーのクライアント Id)。`[Match]` のログの `player=` / `killer=` / `victim=` と同じ番号 |
| `productUserId` / `killerProductUserId` / `victimProductUserId` | EOS の ProductUserId。`-transport eos` のサーバーにつないだ人だけに付く (UDP では出さない) |

| `type` | いつ | 続く項目 |
| --- | --- | --- |
| `server_started` | 待ち受けを始めた | `version` (ビルドの版)、`transport` (`udp` / `eos`)、`endpoint` (待ち受けたアドレスとポート、EOS なら `eos`) |
| `server_stopping` | 終了を求められた (`docker stop` の SIGTERM など) | なし |
| `player_joined` | プレイヤーが試合に入った | `matchId`、`playerId`、`productUserId`、`team` (`Alpha` / `Bravo`)、`rejoined` (切断から戻り、前の成績を引き継いだなら `true`) |
| `player_left` | プレイヤーが試合から抜けた (切断を含む) | `matchId`、`playerId`、`productUserId` |
| `match_started` | 両チームがそろって試合が始まった | `matchId`、`players` (試合に入っている人数) |
| `kill` | プレイヤーが倒された | `matchId`、`killerId`、`killerProductUserId`、`victimId`、`victimProductUserId`、`teamKill` (味方に倒されたなら `true`)、`assisterIds` (アシストした人の Id をカンマ区切りで、与えたダメージの多い順。いなければ出さない)。自滅では `killerId` と `victimId` が同じ |
| `match_ended` | 試合が終わった | `matchId`、`winner` (`Alpha` / `Bravo`、引き分けなら `draw`)、`scoreAlpha`、`scoreBravo` |
| `ping_placed` | プレイヤーが印を付けた (ルールの `ping` が `On` のときだけ)。印は同じ陣営の人にだけ配る | `matchId`、`playerId`、`productUserId`、`team`、`pingId` (試合の中で一意の印の番号。`[Ping] net` のログの `id=` と同じ)、`kind` (`Enemy` / `Location`)、`targetId` (敵の印なら印の付いた敵のプレイヤー Id)、`refreshed` (同じ敵に付け直したなら `true`) |
| `ping_refused` | 印を付けようとして断った。連打でログを埋めないよう、1 秒に 1 行まで | `matchId`、`playerId`、`productUserId`、`reason` (`RuleOff` / `Dead` / `InvalidAim` / `TooSoon` / `NoTarget`) |
| `vehicle_entered` | プレイヤーが乗り物に乗った | `matchId`、`playerId`、`productUserId`、`vehicle` (マップの乗り物の並びの番号)、`vehicleId` (乗り物の Id)、`seat`、`role` (`Driver` / `Passenger` / `Gunner`) |
| `vehicle_left` | プレイヤーが乗り物から降りた・降ろされた | `matchId`、`playerId`、`productUserId`、`vehicle`、`vehicleId`、`seat`、`reason` (`Request` / `Wrecked` / `RiderDied` / `Forced` / `RiderRemoved`) |
| `vehicle_exploded` | 乗り物が壊れて爆発した。残骸は試合の終わりまで残る | `matchId`、`vehicle`、`vehicleId`、`attackerId` (最後に傷めた人。抜けていれば無し)、`attackerProductUserId`、`riders` (乗っていた人数)、`damaged`、`killed` |
| `chat` | プレイヤーのチャットを配った。`All` は全員に、`Team` は送った人と同じチームの人にだけ配る。本文は出さない | `matchId`、`playerId`、`productUserId`、`channel` (`All` / `Team`)、`length` (前後の空白と改行を落とした本文の文字数) |
| `chat_refused` | チャットを断った。断った文は送った人にも返さない。連打でログを埋めないよう、1 秒に 1 行まで | `matchId`、`playerId`、`productUserId`、`reason` (`Empty` / `TooLong` / `TooSoon`) |
| `map_vote` | プレイヤーが次のマップに投票した (選び直しも 1 行) | `matchId`、`playerId`、`productUserId`、`mapId` |
| `map_changed` | 次の試合のマップを替えた | `matchId` (終わった試合)、`fromMapId`、`toMapId` |
| `team_balanced` | 人数をそろえるため、次の試合の前にプレイヤーのチームを移した。移した人ごとに 1 行 | `matchId`、`playerId`、`productUserId`、`from`、`to` (`Alpha` / `Bravo`) |

試合が終わって次の試合が始まるとき、残っている人には `player_joined` を出し直さない。次の試合の顔ぶれの人数は `match_started` の `players` で分かる。
`map_vote`・`map_changed` は「試合の合間のマップの投票」、`team_balanced` は「チームの人数をそろえる」、`chat`・`chat_refused` は「チャットの上限」の設定で出る。
標準出力に書けなくなったとき (受け手のパイプが閉じたなど) は `[Net] cannot write match events to stdout (...)` を一度だけ出し、以後は JSON の行を出さない。試合は止めない。

## CI のテンプレート

サーバーの CI は、キットではテンプレートとして `templates/github-workflows/` に置き、このリポジトリでは走らせない。
ゲームのリポジトリで使うものを `.github/workflows/` に写す。写せばそのまま動き、名前を変えたいものはリポジトリの変数で渡す。

```sh
mkdir -p .github/workflows
cp templates/github-workflows/server-image.yml .github/workflows/
cp templates/github-workflows/map-data.yml .github/workflows/
```

- `server-image.yml`: `v*` のタグを push するとサーバーのイメージを `linux/amd64` と `linux/arm64` で作り、
  タグ名そのもの (`v1.2.3`)、サーバーが `[Version]` で出す版 (`1.2.3`、`v1-rc` なら `1-rc`) と、semver なら `1.2` のタグで push する。
  版はタグ名から渡し、git describe は使わない。版にできないタグ名 (`+` を含むものなど) ではビルドの前に失敗する。
  Unity のライセンスもビルドも使わない。push 先は変数 `SERVER_IMAGE` (無ければ `ghcr.io/<owner>/<repo>/server`)。
  EOS の SDK は `scripts/eos-sdk.sh --fetch` で取るので、EOS のライブラリは amd64 のイメージにだけ入り、arm64 のイメージは UDP だけで待ち受ける。
  push する前に amd64 のイメージを起動し、`[Version] <版>` と `[Net] server listening on 0.0.0.0:7777` を出し、uid 10001 で動き、
  SIGTERM で終了コード 0 で止まることを確かめる (`scripts/server-image-smoke.sh`。手元でも `make server-image` の後に
  `bash scripts/server-image-smoke.sh shooter-server:local` で同じことを確かめられる)。PR でもここまでを push せずに走らせる。
  要らなければ `on:` の `pull_request` を消し、ファイルを絞るなら `paths` を足す。
- `map-data.yml`: 毎晩 Unity でマップのデータ (`server-data/maps`) を書き出し直し (`scripts/nightly-server-data-ci.sh`)、
  コミットしたものと違えば失敗する。[GameCI](https://game.ci/docs/github/activation) の手順で作った Secrets の `UNITY_LICENSE` が要る。
  手元では `make nightly-local` が同じことを確かめる。失敗したら、シーンを変えたのにマップのデータを書き出していない。
  `make export-server-data` で書き出してコミットする。

```sh
docker pull ghcr.io/<owner>/<repo>/server:v1.2.3
```
