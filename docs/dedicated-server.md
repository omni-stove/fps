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

止めるときは `docker stop -t 700 fps-server`。サーバーは SIGTERM を受けてもすぐには止まらず、今の試合が終わるのを待つ
(次の「試合が終わってから止める」)。`-t` を付けないと docker は 10 秒で SIGKILL を送るので、試合の途中で落ちる。
サーバーは止まる前につないでいるクライアントへ止まると知らせて切り (`[Net] server stopping; told <人数> client(s) and disconnected them`)、
クライアントは「サーバーが止まりました。」を出してメインメニューに戻る。知らせが届かずに切れたとき (サーバーが落ちた・回線が切れた) は、
クライアントは Netcode が切断に気づいたところで「サーバーからの応答が途絶えました (タイムアウト)。」を出して戻る。
クライアントのログには `[Net] disconnected from server id=<Id> kind=ServerStopped connected=True reason="fps:server-stopped"` のように理由が出る。

### 試合が終わってから止める

専用サーバー (`-mode server`) は終了を求められると (SIGTERM。`docker stop`、`docker kill -s TERM`、`kill <PID>`)、
「止まる準備中」になる。

1. EOS のロビーに登録していれば (`-register-lobby`)、すぐにロビーを閉じ、クイックマッチで選ばれないようにする。
2. 新しく来たクライアントを承認で断る。サーバーには `[Net] rejected client <Id>: the server is draining before it stops` が出て、
   クライアントは「サーバーが止まる準備中のため参加できません。別のサーバーにつないでください。」を出してメインメニューに戻る
   (ログは `kind=Rejected connected=False detail=server-draining reason="fps:rejected:server-draining"`)。
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
docker stop -t 700 fps-server                                   # 止めるたびに指定する
docker run --rm --stop-timeout 700 -p 7777:7777/udp fps-server:local   # コンテナを作るときに決めておく
docker compose -f docker/server/compose.yaml stop                # stop_grace_period: 700s を書いてある
docker kill -s TERM fps-server; docker kill -s TERM fps-server  # 2 回送るとすぐ止まる
```

`-drain-timeout` を変えたら猶予もそれに合わせる (例: `-drain-timeout 1800` なら `-t 1810` 以上)。
Kubernetes では `terminationGracePeriodSeconds` を同じように決める。

### 手元のクライアントからつなぐ

UDP の専用サーバーにつなぐクライアントは、通常のプレイヤーのビルド (`make build`) を `-mode client` で起動する。

```sh
Builds/client-macos/fps.app/Contents/MacOS/fps -mode client -address 127.0.0.1
```

ポートを変えたときは `-port` も合わせる。別のマシンのサーバーには `-address` にそのマシンの IPv4 アドレスを渡す。

### NetArena01 で拠点制圧 (Domination) を遊ぶ

マップは起動時に `-map` で選ぶが、試合のルールは試合の進行役のプレハブ (`NetMatch.prefab`) に焼いてあり、起動時には選べない。
コミットしてあるのはチームデスマッチのルールなので、拠点制圧を遊ぶときはルールを `Domination.asset` にしたプレハブを作り、
サーバーとクライアントを両方ビルドし直す。マップ NetArena01 には拠点 A (Alpha の陣地寄り)・B (中央)・C (Bravo の陣地寄り) が
置いてあり、チームデスマッチのときは使われない。

Unity の Editor を閉じてから、リポジトリのルートで次を実行する。

```sh
# 1. 試合の進行役のルールを Domination にする (NetTest のシーンとプレハブを作り直す)
unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.NetSceneBuilder.BuildWithDominationRules
# 2. 拠点の入った NetArena01 を作り直す (ServerBuilder はマップがあれば作り直さないので、古いマップが手元にあれば必ず実行する)
unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.ArenaSceneBuilder.Build
# 3. 同じシーンとプレハブでサーバーのイメージとクライアントを作る (拠点の NetworkObject の ID がそろっていないとつながらない)
make server-image
make build
# 4. NetArena01 で起動する (イメージ名の後ろの引数はサーバーにそのまま渡る)
docker run --rm --name fps-server -p 7777:7777/udp fps-server:local -map NetArena01
```

イメージを作らずにビルドしたサーバーを直接動かすときも、`-map NetArena01` を足すだけでよい。

```sh
Builds/server-linux/fps-server.x86_64 -logFile - -mode server -map NetArena01
```

試合が始まると、サーバーのログに拠点の数と、占領・中立化のたびに次の行が出る。

```
[Domination] net points=3 scoreLimit=... captureSeconds=... pointsPerSecond=...
[Domination] captured point=A team=Alpha
[Domination] neutralized point=A from=Alpha
```

2 つのクライアントを `-mode client -address <サーバーのアドレス>` でつなぐと、それぞれ別のチームに入る。HUD に A・B・C の
3 つの拠点が並び、拠点の床の円と HUD の文字が持ち主のチームの色で塗られる。拠点の中心には旗が立っていて、遠くからでも場所がわかる。
円の中に立つと占領が進み、両チームがいると止まる。

確かめ終えたら、次でコミットしてあるチームデスマッチのルールに戻す (`NetMatch.prefab` と NetTest の差分が消える)。

```sh
unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.NetSceneBuilder.Build
```

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

## 試合のイベントを JSON で出す

`-log-format json` を付けると、今までのログ (`[Net]` / `[Match]` 付きの行) に加えて、サーバーの起動・終了と試合のイベントを
1 イベント 1 行の JSON で標準出力に出す。付けないとき (`-log-format text`、既定) はこれまでのログのままで、JSON の行は出ない。
`-log-format json` は `-mode server` でだけ使え、Host やクライアントに付けると `[Net] invalid arguments: -log-format json is only used by -mode server (got host)` で終わる。

```sh
docker run --rm --name fps-server -p 7777:7777/udp fps-server:local -log-format json
```

同じ標準出力に今までのログの行も混ざるので、jq では各行を文字列として読み (`-R`)、JSON として読めて `type` のある行だけを拾う。

```sh
docker logs fps-server 2>&1 | jq -cR 'fromjson? | select(.type?)'
docker logs fps-server 2>&1 | jq -cR 'fromjson? | select(.type? == "kill")'
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
| `kill` | プレイヤーが倒された | `matchId`、`killerId`、`killerProductUserId`、`victimId`、`victimProductUserId`、`teamKill` (味方に倒されたなら `true`)。自滅では `killerId` と `victimId` が同じ |
| `match_ended` | 試合が終わった | `matchId`、`winner` (`Alpha` / `Bravo`、引き分けなら `draw`)、`scoreAlpha`、`scoreBravo` |
| `ping_placed` | プレイヤーが印を付けた (ルールの `ping` が `On` のときだけ)。印は同じ陣営の人にだけ配る | `matchId`、`playerId`、`productUserId`、`team`、`pingId` (試合の中で一意の印の番号。`[Ping] net` のログの `id=` と同じ)、`kind` (`Enemy` / `Location`)、`targetId` (敵の印なら印の付いた敵のプレイヤー Id)、`refreshed` (同じ敵に付け直したなら `true`) |
| `ping_refused` | 印を付けようとして断った。連打でログを埋めないよう、1 秒に 1 行まで | `matchId`、`playerId`、`productUserId`、`reason` (`RuleOff` / `Dead` / `InvalidAim` / `TooSoon` / `NoTarget`) |

試合が終わって次の試合が始まるとき、残っている人には `player_joined` を出し直さない。次の試合の顔ぶれの人数は `match_started` の `players` で分かる。
標準出力に書けなくなったとき (受け手のパイプが閉じたなど) は `[Net] cannot write match events to stdout (...)` を一度だけ出し、以後は JSON の行を出さない。試合は止めない。

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
