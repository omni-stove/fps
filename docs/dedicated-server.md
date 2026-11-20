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
| `BattleRoyale` | `BattleRoyale` | `server-data/tuning/battle-royale.json` | エリア (`zone`) |
| `Extraction` | `Extraction` | `server-data/tuning/extraction.json`・`stash.json` (起動に `--stash-dir`) | 脱出地点 (`extractionPoints`) 1 つ以上 |

- マップに要るものが無いと、起動のときにマップの名前と足りないものを添えたエラー (`[Server] failed to start: ...`) で終了コード 1 になる。`mapPool` の他のマップも起動のうちに確かめる。
  例のマップ (`NetArena01`・`NetTest`) には両チームの旗の台 (`FlagBase`) を自陣の出現地点の奥に置いてあり、`make export-server-rules` で `<map>.points.json` の `flagBases` に書き出す。
  台を置く前に作ったシーンには、`make export-server-data`・`make export-server-rules` が書き出す前に台を足して保存する (台が 1 つでもあるシーンには触らない)。
  台の円盤の大きさは書き出すときの `captureTheFlag.captureRadius` に揃える。運び込んだかの判定はサーバーがルールの値で決める。
- `Payload` の道筋は、シーンの `PayloadCart` の道筋の点・中継点と、台車の範囲 (`ControlPointZone`、無ければ既定の半径 5m・高さ 6m) を
  `make export-server-rules` が `<map>.points.json` の `payload` に書き出す。攻め側はシーンの `PayloadRunner` の攻め側 (無ければ Alpha)。
  例のマップでは `NetArena01` にだけ台車を置いてあり、置く前に作ったシーンには旗の台と同じく書き出す前に足して保存する。
  道筋の無いマップ (`NetTest` など) を `-rules Payload` で選ぶと起動しないので、`make server-dotnet ARGS="-rules Payload -map NetArena01"` で立てる。
  Bot は台車のまわりを巡って押し・止め、`[Payload] cart=pushing`・`checkpoint=`・`state=Ended ... winner=` がログに出る。
- `Payload` の進め方は `match.payload` に書く。持ち時間は `timeLimitSeconds` から始まり、中継点を通るたびに `checkpointBonusSeconds` 足す。
  台車の範囲に攻め側が `minPushers` 人以上いて守り側がいなければ、`cartSpeed` に `pusherSpeedMultipliers` の倍率 (`minPushers` 人目からの人数ごと、超えた分は最後の値) を掛けた速さで進む。
  範囲に守り側もいれば止まる。攻め側が範囲に誰もいないまま `rollbackDelaySeconds` 過ぎると、`rollbackSpeed` で最後に通った中継点 (無ければ始点) まで戻る。
  終点に着けば攻め側、時間が切れれば守り側の勝ち。

  ```json
  "match": {
    "respawnSeconds": 5,
    "payload": { "cartSpeed": 1.5, "minPushers": 1, "pusherSpeedMultipliers": [1, 1.5, 2], "rollbackDelaySeconds": 10, "rollbackSpeed": 0.5, "checkpointBonusSeconds": 60, "timeLimitSeconds": 300 }
  }
  ```

- `SearchAndDestroy` のラウンドに人ごとの財布を付けるには、買える品の一覧 `server-data/tuning/economy.json` を置く。
  書き方はクライアントの `Resources/Tuning/economy.json` と同じで (`items` の `id`・`kind`・`price`・`slot`・`nameKey` と `resetOnSwap`)、
  `make export-server-rules` は書き出さないので手で置いてコミットする。一覧が無ければ経済は動かず、`[Economy] off: no buy list ...` と出る。
  例の一覧はルールの JSON にある 2 つの武器 (どちらもロードアウトの既定の武器) だけを売るので、そのままでは持ち替える物が無い。売る武器はルールの JSON の `catalog` に足してから一覧に載せる。
  額 (始めのお金・上限・キル・ラウンドの勝ち・負け・連敗の上乗せ) はルールの JSON の `match.economy` に書き、一覧の額の欄は読まない。

  ```json
  "match": {
    "economy": { "startMoney": 800, "maxMoney": 16000, "killReward": 300, "roundWinReward": 3000, "roundLossReward": 1400, "lossStreakBonus": 500, "maxLossReward": 3400 }
  }
  ```

  - 財布は試合の始めに `startMoney` で作り、キルでその場で、ラウンドの終わりに勝ち負けと連敗の数で払う。`resetOnSwap` が `true` なら攻守の入れ替えで `startMoney` に戻す。
  - 買えるのはラウンドの準備の間 (`searchAndDestroy.preparationSeconds`) だけで、生きている人の武器 (`kind` が `Weapon`) だけ。
    品の Id は同じルールの JSON の `loadout` か `catalog` にある武器の Id にし、`slot` はロードアウトの枠の番号にする。
    持たせられない品 (投げ物・防具・ルールに無い武器・枠の外) は、経済を始めるとき (`[Economy] started ...` の前) に `[Economy] item=... is not sold`・`the weapon ... cannot be equipped` と出して売らない。
  - 買った武器は倒されるまで持ち続け、倒されると次の出現からロードアウトの武器に戻る。買った人には答え (`BuyAnswer`) を返し、断った理由は `[Economy] player=... refused ... verdict=` に出る。
  - 経済は `SearchAndDestroy` の目的でだけ動く。他の目的では一覧を置いても財布を作らない。
- ドローン (置ける物の `role` が `Drone`) と盾 (武器の `shield`) はオフラインだけのもので、専用サーバーでは使えない。
  - `deployables.json` に `Drone` の種類があると、起動のときに `[Deploy] net kind=<id> unusable: a Drone is piloted only offline; ...` と出し、
    置く要求を断る (`[Deploy] ... net refused reason=Unusable`)。
  - サーバーは盾を出さず、盾の武器も普通の武器として撃たせてしまうので、盾の武器はルールの JSON の `loadout`・`catalog` と `economy.json` に入れない。
  - 暗視・サーマル (照準の `visionMode` とゴーグル) もオフラインだけで、ネット対戦では効かない。
- `make export-server-rules` は、シーンの箱 (`LootContainer`) を `<map>.points.json` の `lootContainers` (`id`・`table`・`position`・`reach`) に、
  脱出地点 (`ExtractionZone`) を `extractionPoints` (`id`・`center`・`radius`) に、縮むエリア (`ShrinkingZone`) の最初の円を `zone` (`center`・`radius`) に、
  パラシュートで降ろす `BattleRoyaleRunner` の高さを `dropHeight` に書き出す。どれも置いていないマップでは項目ごと書かない。
  `id` は部品の `Container Id`・`Point Id` から取り (オブジェクトの名前は使わない)、空や重なりがあると書き出しを止める。エリアが 2 つ以上でも止める。
  箱の中身は試合の始まりに `server-data/tuning/loot.json` (クライアントの `Resources/Tuning/loot.json` と同じ中身) の表の `table` から引き、
  ログの `[Loot] seed=` の種が同じなら同じ中身になる。表に無い `table` の箱は空のまま開く (`[Loot] table=... is not in loot.json`)。
  箱を開けられるのは、届く距離 (`reach`) の内に立つ生きている人で、目的が開けることを許すときだけ。断った理由は `[Loot] open ... refused=` に出る。
  箱を置くのは箱を開ける目的 (`BattleRoyale`・`Extraction`) の試合だけで、ほかの目的では箱のあるマップでも `[Loot] placed boxes=0 map=... (objective ... opens no boxes)` になる。
- 持ち物の使う・捨てる・付け外し・持ち物からの持ち替えは、撃てる人 (試合中で、生きていてダウンしておらず、ラウンドの合間でない人) にだけ許す。
  断った理由は `[Inventory] use|drop|attach|detach|equip ... rejected=` に出し (`Dead`・`Downed`・`BetweenRounds`・`NotPlaying`・`NotInMatch` か、`Full`・`NotHeld` などの中身の理由)、
  同じ人の断りは 1 秒に 1 行にする。並べ替え (`move`) は持ち物を増やしも減らしもしないので、倒れている間も許す。一覧に無い Id はログに出さず `-` にする。
- `Extraction` の目的 (`server-data/rules/Extraction.json`) は持ち物庫をアカウントごとにディスクに置くので、`--stash-dir <フォルダ>` を付けて立てる。
  付けないと `[Server] the Extraction rules keep each account's stash on disk; pass --stash-dir ...` で終了コード 3 になる (プレイリストの項目にあっても同じ)。
  試合の途中で次のルールを Extraction に替えようとしたときも、同じ理由を添えて替えずに断る。
  持ち物庫は `<フォルダ>/<アカウントの ID>.json` (Unity の持ち物庫と同じ `StashSave` の JSON) で、ID は承認の鍵 (EOS でつないだ人は ProductUserId)、Bot は `bot.<番号>`。
  鍵を送らない人の持ち物庫は保存しない。読めないファイルは `.broken` を付けて退け、`startingItems` から始める。
  出撃の時間・脱出・持ち物庫の広さは `server-data/tuning/extraction.json`・`stash.json` (クライアントの `Resources/Tuning/` と同じ中身) から読む。
  出現で、選んだ武器 (選んでいなければ既定の並び) のうち持ち物庫にある物だけを持ち込み、持ち物庫から取る。開いた脱出地点で待ち切ると、
  持ち込んだ物と箱から入った物のうち今も持っている物を持ち物庫に入れて保存する (`[Extraction] ended outcome=Extracted ... stored=`・`[Stash] saved ...`)。
  倒れるか抜けると持っている物を失い、その場に箱が残る。全員の出撃が終わるか時間が切れると試合を終え、倒れた人は次の試合まで出直さない。
  Bot は近い箱へ向かって開け、何か入ったら開いている脱出地点へ向かう。
- `BattleRoyale` (`server-data/rules/BattleRoyale.json`) の分隊の数と人数・制限時間・エリアの段階 (`zone.phases`)・物資投下 (`supplyDrops`)・
  復帰 (`redeploy`) は `server-data/tuning/battle-royale.json` から読む (無ければ Core の既定。節の項目はクライアントの `supply-drops.json`・`redeploy.json` と同じ)。
  最初の円と降ろす高さはマップの `zone`・`dropHeight` を使い、`zone` の無いマップは起動しない。入った順に分隊へ振り分け、同じ分隊の人には当たらない。
  始まりに全員の武器を空にして `dropHeight` からパラシュートで降ろし (`[BattleRoyale] drop`)、エリアの外の人に段階のダメージを与え (`[Zone] outside`)、
  次の円の中へ物資投下の箱を落とし (`[SupplyDrop] landed`)、倒れきった人の持ち物を箱にし、最後の分隊が残ると `[Mode] placements winner=` を出して終える。
  ダウン・蘇生は分隊の味方で数え、復帰は倒されたときの段階と回数で決まる (`[Mode] redeploy ... verdict=`)。
- 例のマップ `NetBattleRoyale` (160m 四方) は Battle Royale と Extraction のためのマップで、チームを決めない出現地点 8 つ、箱 12 個 (`net-br-box-01`〜`12`)、
  脱出地点 4 つ (`net-br-exit-north`・`east`・`south`・`west`)、半径 75m のエリアを置いてある。シーンは `make build-scenes` (`NetSceneBuilder.BuildBattleRoyale`) が作る。
  箱の `table` は Demo の Extraction のシーンと同じ `demo-supplies`・`demo-valuables` で、キットの `loot.json` には無いので、ゲームが `loot.json` に表を足すまで箱は空のまま開く。
  箱と脱出地点の見た目はクライアントがスナップショットから出すので、シーンの部品は形も当たりも持たない書き出しの印にしてある。
  `make export-server-rules` は `BattleRoyale`・`Extraction` のルール (`rules.objective` が `BattleRoyale`・`Extraction`) を NetMatch の並びに無くても書き出し、
  `loot.json`・`supply-drops.json`・`redeploy.json` を `server-data/tuning` に写す。ルールのアセットの目的が違うと書き出しを止めるので、`make build-scenes` で作り直す。
  Bot は人が 1 人もいないと置かれないので、`make server-dotnet ARGS="-rules BattleRoyale -map NetBattleRoyale -bots 7"`・
  `make server-dotnet ARGS="-rules Extraction -map NetBattleRoyale -bots 7 --stash-dir ./stashes"` で立ててクライアントを 1 人つなぐと、
  Bot が `[BattleRoyale] state=Ended winner=`・`[Extraction] ended outcome=Extracted ... account=bot.<番号>` まで進む。
  `loot.json` に表が無く箱が空のままだと Bot は武器を拾えないので、BR の決着はエリアのダメージと `battle-royale.json` の `timeLimitSeconds` に頼ることになる。
- `GunGame` の段の武器 (`tierWeaponIds`) は、同じ JSON の `loadout` か `catalog` にある武器の Id にする。無い Id があると起動しない。
  近接で倒した相手の段を下げるには、`meleeWeaponIds` の先頭に近接の Id を書く。サーバーの近接はその Id で倒したことにする。
- 近接 (`rules.actions.melee`)・ストリーク (`rules.streaks`)・キルカム (`rules.deathCamera` が `KillCam`) はどのモードでも使え、
  モードの例はどれも近接とキルカムを `On` にしてある (`GunGame` はストリークを `Off`)。この 3 つの規則はスナップショットでクライアントにも届く。
- 近接・ストリークの調整値は `server-data/tuning/melee.json`・`streaks.json` から読む。クライアントの `Resources/Tuning/` と同じ中身にしておく。
  置ける物 (タレット・盾・囮・ビーコン) は `server-data/tuning/deployables.json` にある種類だけを置かせる。
  種類ごとの `maxCharges`・`rechargeSeconds`・`cooldownSeconds` (Unity の能力のアセットの回数・戻り・間隔と同じ値にする) で人ごとに置ける回数と間隔を数え、
  使い切ると `[Deploy] ... refused reason=NoCharges charges=0/<回数> wait=<秒>`、間隔の内なら `reason=CoolingDown` で断る。回数は出現し直すと満ちる。
- 箱を開ける要求は、水平の届く距離に加えて足元と箱の高さの差が 2 m 以内で、体から箱までの線が壁に遮られないときだけ受ける
  (`[Loot] open ... refused=OutOfReach`・`refused=OutOfSight`)。ストリークの要求は `streaks.json` の報酬の表に無い Id を `reason=UnknownReward` で断る。
- リスポーンビーコン (置ける物の `role` が `Beacon`) は、ルールの `respawnAnchor` に `Beacon` があるときだけ復活の場所になる。
  倒されて戻る人を `respawnAnchor` の順に試し、置いた本人か味方の、使える中でいちばん後に置いたビーコンの前 (`beacon.spawnDistance`) へ出す
  (`[Beacon] ... used by=<id> ... uses=<残り>/<回数>`・`[Match] spawn player=<id> ... from=Beacon`)。回数 (`beacon.uses`) を使い切るか壊されると消え、拠点に戻る。
  入った時と試合・ラウンドの頭は拠点から出す。`Squad` (味方の近く) はサーバーではまだ選ばず、次の順へ進める。
  例のルール (`server-data/rules`) はどれも `abilities` が `Off`・`respawnAnchor` が `["Base"]` なので、使うにはルールの JSON の両方を替える。
- 文字のチャット (`rules.textChat`)・観戦 (`rules.spectating`)・置ける物 (`rules.actions.abilities`) もどのモードでも止められる。
  `Off` にすると、サーバーはチャットを誰にも配らず (`[Chat] refused ... reason=RuleOff`)、観戦の要求に映す相手を割り当てず (ラウンドの合間も)、
  置く要求を断る (`[Deploy] ... refused reason=RuleOff`)。既定は `textChat` と `spectating` が `On`、`abilities` が `Off` で、
  置ける物を使うには `deployables.json` に加えて `abilities` を `On` にする。この 3 つの規則もスナップショットでクライアントに届く。

### プレイリストで回す

`-playlist <id>` で、`server-data/playlists/*.json` (Unity の `Resources/Tuning/playlists.json` と同じ書き方) のプレイリストに沿って試合ごとにモードとマップを替える。
項目の `scene` は `server-data/maps/` のマップ名、`rules` はルール名 (省くと `TeamDeathmatch`)、`mutators` は `server-data/tuning/mutators.json` の Id にする。
`server-data/playlists/rotation.json` に TDM (`NetArena01`) → Domination (`NetTest`) → Payload (`NetArena01`) の例を置いてある。
台車の道筋は `NetArena01` にしか無いので、Payload の項目は `NetArena01` に置く。

```sh
make server-dotnet ARGS="-playlist rotation"
```

- 項目がルールとマップを決めるので、`-rules`・`-map` とは一緒に渡せない (終了コード 2)。
- 最初の項目で始め、試合が終わるたびに次の項目へ進む (最後の後は最初に戻る)。`order` が `Shuffle` なら起動ごとに並びを混ぜる。
- 試合の設定 (`match.*` の点数・時間・待ち時間・`mapVoteSeconds` など) と武器は最初の項目のルールの JSON のものを使い続け、2 項目目からは `rules.*` (モードと規則) だけを替える。
  `match.voteKick`・`match.payload`・`match.economy`・`match.searchAndDestroy` も最初の項目のものなので、後の項目で使うモードの節も最初の項目の JSON に書く。
  放置 (`rules.idleKickSeconds`・`idleWarningSeconds`) は `rules.*` なので項目ごとに替わり、替わるたびに全員の放置の時計を戻す。
- 経済は `SearchAndDestroy` の項目でだけ財布を作り、次の項目へ移ると財布と買った武器を捨てる。買える品の一覧 (`tuning/economy.json`) の武器は最初の項目の `loadout`・`catalog` から引く。
- ミューテーターは、項目のルールの `rules` に `mutators.json` の各 `rules` を並びの順に重ねたもの (同じ欄は後が勝つ) で、その項目の間だけ効く。
  `make export-server-rules` は `mutators.json`・`playlists/*.json` を書き出さないので、クライアントの `Resources/Tuning/mutators.json`・`playlists.json` から写してコミットする
  (`server-data/tuning/mutators.json` が無ければミューテーターは空で、`mutators` を書いた項目は起動しない)。

  ```json
  { "mutators": [ { "id": "low-health", "rules": { "playerMaxHealth": 50 } } ] }
  ```

  ```json
  { "playlists": [ { "id": "sd-rotation", "order": "Sequential", "entries": [
    { "scene": "NetArena01", "rules": "Payload" },
    { "scene": "NetTest", "rules": "SearchAndDestroy", "mutators": ["low-health"] }
  ] } ] }
  ```
- `mapVoteSeconds` が 0 なら、項目のモードはその項目のマップで遊べればよい (次の試合のマップは項目のマップに決まるため)。
  投票するなら、どの項目のモードも、プレイリストに出てくるすべてのマップ (と最初のルールの `mapPool`) で遊べなければならない。
  足りないと待ち受ける前に `[Server] failed to start: playlist <id> entry <n>/<数> cannot use rules ...` で終了コード 1 になる。
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

### 投票キックと放置

投票キックは専用サーバーだけで動き、ルールの JSON の `match.voteKick` で決める。既定は `enabled: false` で、持ち主が選ぶまで他の人を外す口を開けない。

```json
"match": {
  "voteKick": { "enabled": true, "requiredYesFraction": 0.6, "voteSeconds": 30, "cooldownSeconds": 120 }
}
```

- 試合にいる人 (Bot は除く) が相手を選んで言い出す (Demo ではスコアボードから)。言い出した人は賛成に数え、票を入れられるのは対象の人を除いた全員。
  賛成が `requiredYesFraction` (0 より大きく 1 以下) の割合 (切り上げ、最低 1 票) に届くとその場で決まり、`voteSeconds` の間に届かなければ外さない。
- 進んでいる投票は 1 つだけ。同じ人は `cooldownSeconds` の間、次の投票を言い出せない (つなぎ直しても承認の鍵で数える)。Bot と自分は対象にできない。
- 外すと決まったら、結果を全員に送ってから対象の人を切る (`[Net] dropped client <番号>: vote kicked (yes 2/2)`)。クライアントはメニューに投票で外されたことを出す。
  その人は今の試合が終わって次の試合が始まるまで入り直しを断られ (`vote kicked from match ...; refused until the next match`)、次の試合で入れる (`[VoteKick] next match; 1 vote-kicked account(s) may join again`)。
  BAN の一覧には載せない。承認の鍵を送らない人は見分けられないので、切るだけで入り直しは断れない。
- 起動のときに `[VoteKick] on yesFraction=... seconds=... cooldown=...` (切っていれば `[VoteKick] off`) が出る。言い出し・票・結果は `[VoteKick] call`・`vote`・`decided` に出る。

放置で切るのはルールの JSON の `rules.idleKickSeconds` (0 なら切らない) と `rules.idleWarningSeconds` で決める。

```json
"rules": { "idleKickSeconds": 120, "idleWarningSeconds": 15 }
```

- 動く・向きを変える・跳ぶ・走る/しゃがむ/構える/覗き込むの切り替えなどの入力が `idleKickSeconds` 秒無い人を切り、残りが `idleWarningSeconds` 秒になったら本人に警告を送る (`[Match] idle warning player=...`)。
  `idleWarningSeconds` は 0 より大きく、`idleKickSeconds` より短くする。
- 数えるのは試合中に生きている間と、倒れた後に味方を観戦している間だけ。ダウンしている間、観戦せずに出直しを待つ間、試合の合間、Bot は数えない。
- 切ると `[Match] idle kick player=... after=...s` が出て、クライアントはメニューに放置で切られたことを出す。投票キックと違って、すぐ入り直せる。

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
- `--stash-dir <フォルダ>` で Extraction の持ち物庫を置く。無ければ作り、書けなければ終了コード 3。docker ではボリュームにする (`-v ./stashes:/stashes` と `--stash-dir /stashes`)。
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
