# 導入ガイド

キットを新しいゲームに入れ、武器・ルール・マップ・見た目を差し替えて、オフラインとオンラインで動かすまでの手順。
欄の意味は各型の Inspector の Tooltip、コマンドの引数は `mise tasks`、専用サーバーの細かい決まりは [dedicated-server.md](dedicated-server.md) を見る。

- `make <task>` と `mise run <task>` は Unity を batch mode で動かすので、Editor を閉じてから打つ。同じことをする Editor のメニューも併せて書く。
- `make` は `-` で始まる引数を食べ、空白を含むパスも分けてしまうので、そういう引数は `mise run <task> ...` で渡す。

## 1. 入れる

1. `Packages/manifest.json` の `dependencies` に足し、`Packages/packages-lock.json` もコミットする。

   ```json
   "com.omnistove.shooter-kit": "https://github.com/omni-stove/shooter-kit.git?path=/Assets/game#v0.1.0"
   ```

2. **Edit > Project Settings > Player > Active Input Handling** を `Input System Package (New)` か `Both` にする。
3. **Window > Package Manager** で Shooter Kit の **Samples** から **Demo Game** を **Import** する。
   取り込み先は `Assets/Samples/Shooter Kit/<版>/Demo Game/` (以下 `<Demo>`。このリポジトリでは `Assets/Samples/ShooterKitDemo/` で、中身は `Assets/game/Samples~/Demo`)。
4. **Shooter > Build All Scenes** を一度実行する (`make build-scenes` でもよい)。
5. `<Demo>/Scenes/Bootstrap.unity` (射撃場) を開いて Play する。左クリックで撃ち、`2` と `1` で武器を持ち替えられれば入っている。

Editor のスクリプトが作るアセットとシーンの置き場 (出力先) は、Demo を入れていれば `<Demo>`、無ければ `Assets/game`。各タスクの `--root <dir>` (`-shooterKitRoot`) で替えられる。

## 2. Demo の武器を 1 つ差し替える

射撃場のプレイヤーが持つ 2 つの武器は、`<Demo>/Weapons/ReferenceJson/Carbine556.json` と `<Demo>/Weapons/ReferenceJson/CompactPistol.json` から作られる (`BootstrapSceneBuilder.PlayerWeaponIds` がこの 2 つの `id`)。ここではピストルを差し替える。

1. `<Demo>/Weapons/ReferenceJson/CompactPistol.json` を開き、`id` は変えずに値を書き換える。例えば連射にして弾倉と威力を増やす。

   ```json
   "trigger": { "fireMode": "Auto", "fireInterval": 0.08 },
   "ammo": { "kind": "Magazine", "magazineSize": 40, "reserveAmmo": 120, "reloadSeconds": 2.27,
             "activeReloadWindowStart": 0.5, "activeReloadWindowLength": 0.15, "activeReloadFailSeconds": 0.6,
             "reloadStyle": "Whole", "reloadSecondsPerRound": 0 },
   "impact": { "damage": 45, "falloff": { "startDistance": 0, "endDistance": 50, "minMultiplier": 0.53 }, "structureRadius": 0 },
   ```

2. **Shooter > Build Weapon Assets** を実行する。Editor を閉じているなら次でもよい。

   ```sh
   mise run import-weapons "<Demo>/Weapons/ReferenceJson/CompactPistol.json" --out "<Demo>/Weapons/Reference"
   ```

   `<Demo>/Weapons/Reference/CompactPistol.asset` が GUID を保ったまま書き換わるので、シーンは作り直さなくてよい。
   JSON のどこかが不正なら何も保存せず、ファイルと項目の名前を Console (batch mode では終了コード 1) に出す。
3. `Bootstrap.unity` を Play し、`2` でピストルに持ち替えて左クリックを押し続ける。止めずに撃ち続け、弾倉が 40 発になっていれば差し替わっている。

武器の決まり:

- `id` は作った後に変えない。サーバーとクライアント、見た目のカタログは武器を `id` で突き合わせ、取り込みも `id` の変わる上書きを断る。別の武器にするなら、新しい UUID の `id` と新しいファイル名で足す。
- 名前は `nameKey` にキーを書き、文は `<Demo>/Resources/Localization/en/items.json` と `ja/items.json` の両方に同じキーで足す。
- 項目は `Shooter.Core` の `WeaponSpecJson` と、`<Demo>/Weapons/Json/` の例を見る。盾は `shield`、爆発は `impact` の `explosion` のように部品ごとに書く。

### 自分のゲームの武器

1. `Assets/MyGame/Weapons/Json/` に武器ごとの JSON を置く (ファイル名がアセット名になる)。
2. 一覧のアセットを **Assets > Create > Shooter > Weapon Catalog** で `Assets/MyGame/Weapons/WeaponCatalog.asset` に作る。
3. 取り込む。**Shooter > Import Weapons From JSON** でフォルダを選ぶか、次を打つ。`--catalog` を付けると、取り込んだ武器を `id` で一覧に足す。

   ```sh
   mise run import-weapons Assets/MyGame/Weapons/Json --out Assets/MyGame/Weapons --catalog Assets/MyGame/Weapons/WeaponCatalog.asset
   ```

4. 最初に持つ武器は **Assets > Create > Shooter > Loadout Definition** で作ったロードアウトのスロットに入れる。
5. 一覧とロードアウトを入口の `GameContent` (次の節) の **Weapon Catalog** と **Loadout** に入れる。

## 3. 入口 `GameContent` を作る

キットは、シーンに配線されていない中身を入口のアセット `GameContent` から引く。

1. **Shooter > Create Game Content** を実行する (`make game-content`、置き場を決めるなら `mise run game-content --path Assets/MyGame/Resources/ShooterKit/GameContent.asset`)。
   プロジェクトに候補が 1 つしか無い欄は埋まり、複数ある欄は空のまま Console に候補が出るので Inspector で選ぶ。
2. `GameContent` は `Resources/ShooterKit/` のどこかに 1 つだけ置く。2 つあると名前順で先のものを使い、エラーを出す。
3. **Shooter > Audit Replacements** (`make audit-replacements`) で、武器の `id` ごとの見た目の抜け・言語ごとの文言の抜け・空の必須の参照を点検する。

引く順は次のとおり。Demo のシーンは `GameBootstrap` に武器とルールを直に配線しているので、Demo のシーンでは `GameContent` より配線が勝つ。

1. シーンのコンポーネントに配線した参照
2. `GameContent` の欄
3. `Resources` の決まったパス (調整値は `Resources/Tuning/<名前>.json`)
4. コードの既定値

## 4. ルールを差し替える

- ルールは `GameRulesDefinition` (**Assets > Create > Shooter > Game Rules**) か JSON で書く。JSON の例は `<Demo>/Rules/Json/*.json`、項目は `Shooter.Core` の `GameRulesJson` にある。
- JSON は **Shooter > Import Game Rules From JSON** か次で取り込む。ファイル名がアセット名になり、既存のアセットは GUID を保って中身だけ替わる。

  ```sh
  mise run import-rules Assets/MyGame/Rules/Json --out Assets/MyGame/Rules
  ```

- `<Demo>/Rules/*.asset` は **Shooter > Build Game Rules Assets** がコードの値で作り直すので、自分のルールは自分のフォルダに取り込む。
- 取り込んだルールは `GameContent` の **Rules** か、シーンの `GameBootstrap` の **Rules** に入れる。
- モードごとの点数や時間は `*Definition` (多くは **Assets > Create > Shooter > Modes** の下) に書き、`GameContent` のモードの欄に入れる。

## 5. マップを差し替える

### オフライン

- シーンに `GameBootstrap` と `MatchRunner` を付けた GameObject を置く。自分の床があるなら `GameBootstrap` の **Create Floor** を切る。敵を歩かせるなら NavMesh を焼く。
- モードごとに置く部品 (拠点・旗の台・台車・縮むエリアなど) は、`<Demo>/Scripts/Editor/*SceneBuilder.cs` が組み立てるシーンを見本にする。
- シーンを **File > Build Profiles** の一覧に載せる。メニューから移るシーンの名前は `MenuSceneSettings` (`GameContent` の **Menu Scenes**、空なら `Resources/Menu/MenuSceneSettings`) に書く。

### オンライン

- サーバーが読むマップは、`NetSpawnPoint` (と、モードに要る拠点・旗の台・台車・箱・脱出地点・縮むエリア) を置いたシーン。
- 当たり判定と NavMesh は、書き出すシーンを並べて書き出す。並べなかったマップの `server-data/maps/` のファイルは消えるので、残すマップも並べる。

  ```sh
  mise run export-server-data -mapScenes "Assets/MyGame/Scenes/MyArena.unity,<Demo>/Scenes/NetArena01.unity"
  ```

  毎回並べないなら、Editor のスクリプトから `MapCollisionExporter.UseMapScenes` に一覧を渡す (Demo は `ServerDataMaps` で渡している)。
- 出現位置などの地点 (`server-data/maps/<map>.points.json`) を書き出す `ServerRulesExporter` は、Demo の `BuildSceneList.ServerScenes` のシーンだけを見る。自分のマップを足すときはその一覧に足す。

## 6. 見た目・音・画面を差し替える

- 見た目と音は `GameContent` の「見た目・音」の欄のカタログ (各 `*Catalog`) に入れる。武器のものは武器の `id` ごとに引く。空ならキットがコードで仮の形を作るか、出さない。
- プレイヤーと敵の体は `GameContent` の **Character Rig** (根に Animator)。
- 画面 (HUD・メニュー) はキットに無い。Demo の `Shooter.Runtime.UI` を写して直すか、`GameSignals` と `CombatEvents` を読む自分の画面を作る ([Assets/game/README.md](../Assets/game/README.md) の「画面」)。
- 文言は `<Demo>/Resources/Localization/<言語>/*.json` に、どの言語にも同じキーで書く。

## 7. オフラインで動かす

1. 自分のシーン (か Demo の `<Demo>/Scenes/MainMenu.unity`) を開いて Play する。
2. 書き出したクライアントで試すなら `make build` (Windows は `make build windows`)。載るシーンは Build Profiles の一覧。

## 8. オンラインで動かす

オンラインは専用サーバーにだけつなぐ。サーバーは Unity のアセットを読めないので、変えたら書き出し直してからサーバーを起動し直す。

1. ネット対戦の設定は `NetMatchDefinition` (`<Demo>/Rules/NetMatch.asset`) に書く。ロードアウトと武器の一覧は、`GameContent` にあればそちらが使われる。
2. Editor を閉じて書き出し、コミットする。武器・ルール・調整値を変えたら 2 つ目をやり直す。

   ```sh
   make export-server-data     # マップ → server-data/maps/  (メニューは Shooter > Net > Export Server Data)
   make export-server-rules    # ルール・地点・調整値 → server-data/rules/ ほか (メニューは Shooter > Server Data > Export Rules And Points)
   ```

3. `make server-dotnet` でサーバーを手元に立てる (0.0.0.0:7777)。ルールやマップの選び方は `ServerLaunch` を見る。
4. Editor で `<Demo>/Scenes/MainMenu.unity` を開いて Play し、「ONLINE - QUICK MATCH」を押す。EOS が無ければ `127.0.0.1:7777` につなぐ。
   書き出したクライアントでは `-mode client -address <ホスト> -port 7777` を付けて起動すると、メニューを通らずにつなぐ。
5. Bot は人が 1 人もいないと置かれないので、試すときはクライアントを 1 人つなぐ。

ドローン・盾・暗視とサーマルはオフラインだけで、サーバーでは効かない。

## データで変えられる場所

コードを触らずに、次のデータだけでゲームの中身を替えられる。調整値の JSON は `GameContent` の同じ名前の欄 (`grenade.json` なら **Grenade Tuning**) に入れれば、キットの `Resources/Tuning/` の既定よりそちらを読む。

| 変えるもの | 形 | 置き場・入れる口 |
| --- | --- | --- |
| 武器の性能 | JSON → `WeaponDefinition` | `Weapons/Json/*.json` を `import-weapons` で取り込む (`WeaponSpecJson`) |
| 選べる武器 | `WeaponCatalogAsset` | `GameContent` の **Weapon Catalog**、ネット対戦では `NetMatchDefinition` |
| 最初に持つ武器・能力 | `LoadoutDefinition` | `GameContent` の **Loadout**、シーンの `GameBootstrap` |
| 装備画面で選べる能力 | `AbilityCatalogAsset` と各 `*Definition` (**Assets > Create > Shooter > Abilities**) | `GameContent` の **Ability Catalog** |
| ルール (回復・リスポーン・行動・目的) | JSON → `GameRulesDefinition` | `import-rules` で取り込み、`GameContent` の **Rules** (`GameRulesJson`) |
| モードの点数・時間 | `TeamDeathmatchDefinition` ほか各モードの `*Definition` | `GameContent` のモードの欄 |
| ネット対戦の設定 | `NetMatchDefinition` | `Rules/NetMatch.asset` → `make export-server-rules` |
| ルールの重ね・巡り方 | `mutators.json`・`playlists.json` | `GameContent` の **Mutators Tuning**・**Playlists Tuning** |
| プレイヤーの移動 | `MotorSettings` | `GameContent` の **Motor**、空なら `Resources/Movement/MotorSettings` |
| 敵の性能と動き | `EnemyDefinition` | `GameContent` の **Enemies** (種類ごとに 1 つ) |
| 乗り物 | `VehicleDefinition`・`HelicopterDefinition`・`BoatDefinition`・`AircraftDefinition` | 乗り物の部品 |
| キャンペーン | `MissionDefinition`・`MissionCatalog`・`BossDefinition` | `GameContent` の **Missions** |
| メニューから移るシーン | `MenuSceneSettings` | `GameContent` の **Menu Scenes** |
| 見た目と音 | `PresentationCatalog`・`ViewmodelCatalog`・`WeaponExtrasCatalog`・`AttachmentModelCatalog`・`HitFeedbackCatalog`・`DamageFeedbackCatalog`・`SurfaceImpactCatalog`・`FootstepSurfaceCatalog`・`RagdollCatalog`・`SuppressionCatalog`・`CameraShakeSettings`・`DamageNumberCatalog`・`AimBlockCatalog`・`ItemIconCatalog`・`StreakIconCatalog`・`WaterCatalog`・`ReticleCatalog`・`StatusEffectVisualCatalog`・`FlashOverlayCatalog`・`CosmeticVisualCatalog`・`EmoteCatalog`・`VisionVisualCatalog` | `GameContent` の「見た目・音」の欄 |
| 体 | 根に Animator の Prefab | `GameContent` の **Character Rig** |
| 文言 | `Resources/Localization/<言語>/*.json` | 画面 (Demo では `GameText`・`LocalizedTexts`) |
| 戦闘の調整値 | `grenade.json`・`melee.json`・`knockback.json`・`stagger.json`・`suppression.json`・`aim-assist.json`・`streaks.json`・`assist.json` | `GameContent` の同じ名前の欄 |
| 持ち物・移動の調整値 | `inventory.json`・`traversal.json`・`stealth.json`・`squad.json`・`ping.json`・`key-sharing.json`・`rumble.json` | 同上 |
| Bot | `bots.json` | 同上 |
| 成長・記録 | `progression.json`・`skill.json`・`achievements.json`・`leaderboards.json`・`cosmetics.json`・`replay.json`・`telemetry.json` | 同上 |
| 画面に出す量と色 | `minimap.json`・`nameplates.json`・`palettes.json`・`chat-words.json`・`weather.json`・`graphics.json` | 同上 |
| モードの調整値 | `payload.json`・`economy.json`・`supply-drops.json`・`redeploy.json`・`infected.json`・`extraction.json`・`loot.json`・`stash.json` | 同上 |
| サーバーの調整値 | `server-data/tuning/*.json` | `make export-server-rules` が書き出す。`economy.json`・`mutators.json`・`playlists/*.json` は手で写す |
