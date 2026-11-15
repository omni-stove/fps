# FPS Kit (`com.omnistove.fps`)

一人称・三人称のシューターのゲームプレイ (武器・敵 AI・モード・キャンペーン) と、シーンやアセットを作る Editor のスクリプトをまとめた UPM パッケージ。
ここに書いた手順だけで、空の Unity プロジェクトに入れて、EditMode テスト・武器 JSON の取り込み・smoke の実行まで進められる。
武器・ルール・ミッション・シーン・プレハブの中身と、それを組む Editor のスクリプトと smoke は、パッケージのサンプル **Demo Game** にある ([サンプル (Demo Game)](#サンプル-demo-game) を参照)。

## 必要なもの

| 項目 | 版 |
| --- | --- |
| Unity | 6000.6 (このリポジトリは 6000.6.0f1) |
| `com.unity.ai.navigation` | 2.0.14 |
| `com.unity.inputsystem` | 1.20.0 |
| `com.unity.modules.ai` / `animation` / `audio` / `particlesystem` / `physics` / `uielements` | 1.0.0 |
| `com.unity.test-framework` (テストを回すときだけ) | 1.8.0 |

上の依存パッケージは `package.json` の `dependencies` にあるので、Package Manager が自動で入れる。自分で manifest に書く必要は無い。

- 入力は Input System だけを使う。**Edit > Project Settings > Player > Active Input Handling** を `Input System Package (New)` か `Both` にする。
- 武器の音と光の素材を作る `Fps/Build Presentation Assets` だけは URP (`com.unity.render-pipelines.universal`) のシェーダーを使う。URP が無くてもほかの機能と smoke は動き、武器の音と光が出ないという警告が出るだけ。薬莢と空撃ちの音の素材 (`Presentation/WeaponExtrasCatalog`) も同じく URP のシェーダーを使い、`Fps/Build All Presentation Assets` (`Fps.Editor.PresentationAssetSteps.BuildAll`) で作る。無ければ薬莢と空撃ちの音が出ないという警告が出るだけ。材質ごとの銃痕と着弾の素材 (`Presentation/SurfaceImpactCatalog`) も同じシェーダーを使い、同じ `Fps/Build All Presentation Assets` で作る。無ければ着弾は従来の火花と音になる。

### ネット対戦 (`Fps.NetV2`) と、任意の EOS (`Fps.NetEos`)

ネット対戦のクライアント (`Fps.Protocol`・`Fps.NetV2.Client`・`Fps.NetV2`) は追加のパッケージ無しでコンパイルされ、Unity を使わない .NET の専用サーバー
(リポジトリの `dotnet/Fps.Server`) へつなぐ。サーバーの作り方と動かし方はリポジトリの `docs/dedicated-server.md` にある。

EOS (P2P の通信路・クイックマッチのロビーの検索・アンチチート) を使う `Fps.NetEos` は、`com.playeveryware.eos` があるときだけコンパイルされる
(asmdef の `versionDefines` と `defineConstraints`)。入れなければ、そのアセンブリは無いものとして扱われ、コンパイルエラーにもならない。

| アセンブリ | 有効になる条件 | manifest に足す行 |
| --- | --- | --- |
| `Fps.NetEos` | `com.playeveryware.eos` | `"com.playeveryware.eos": "https://github.com/EOS-Contrib/eos_plugin_for_unity_upm.git"` |

## 導入

プロジェクトの `Packages/manifest.json` の `dependencies` に 1 行足す。パッケージはリポジトリの `Assets/game` にあるので、`?path=/Assets/game` を付ける。

```json
{
  "dependencies": {
    "com.omnistove.fps": "https://github.com/omni-stove/fps.git?path=/Assets/game#v0.1.0"
  }
}
```

### 版の固定

`#` の後ろで取ってくる版を決める。

| 書き方 | 意味 |
| --- | --- |
| `#v0.1.0` | タグ。リリースごとに付けるタグを指す (`package.json` の `version` と同じ番号) |
| `#<コミットの SHA>` | そのコミットに固定する。タグがまだ無い版を試すとき |
| `#main` または省略 | 既定のブランチ。取ってきた時点のコミットが `Packages/packages-lock.json` に記録され、lock の行を消すまで更新されない |

チームで使うなら、タグか SHA を書き、`Packages/packages-lock.json` もコミットする。版を上げるときは `#` の後ろを書き換える。

Unity を開くと Package Manager が取ってきてコンパイルする。Editor のメニューに **Fps** が出れば入っている。
パッケージの実体は `Library/PackageCache/com.omnistove.fps@<hash>/` に置かれ、Unity からは `Packages/com.omnistove.fps/` として見える (読み取り専用)。

## サンプル (Demo Game)

パッケージの本体 (`Fps.Core`・`Fps.Runtime`・`Fps.Runtime.UI`・`Fps.Protocol`・`Fps.NetV2`・`Fps.NetV2.Client`・`Fps.NetEos`、JSON のインポーターなどの `Fps.Editor`) は、サンプルの中身を参照しない。
仮のゲームの中身はサンプル **Demo Game** (`Samples~/Demo`) にあり、ゲーム側が選んで取り込む。

| 中身 | 置き場所 (サンプルのルートから) |
| --- | --- |
| 武器の JSON とアセット | `Weapons/` (`Weapons/Json/` に `Dmr.json`・`Rifle.json`・`Handgun.json`・`Launcher.json`・`Minigun.json`・`Shotgun.json`・`Crossbow.json`・熱で撃つ `EnergyRifle.json`・`AntiMaterial.json`、射撃場で拾える武器の一覧 `Weapons/RangeWeapons.asset`) |
| ルールの JSON とアセット | `Rules/` (`Rules/Json/` に `TeamDeathmatch`・`Domination`・`Range`・`TpsRange`・`CampaignEasy`/`Normal`/`Hard`) |
| ミッション・シーン | `Missions/`・`Scenes/` |
| ネット対戦の試合の設定 (`NetMatchDefinition`。専用サーバーへ書き出す) | `Rules/NetMatch.asset` |
| メニューから移るシーンのルート (`MenuSceneSettings`) | `Resources/Menu/MenuSceneSettings.asset` |
| シーンと仮の素材を組む Editor のスクリプト・smoke・クライアントのビルド・専用サーバーへのデータの書き出し | `Scripts/Editor/` (`Fps.Samples.Editor`)・`Scripts/Net/Editor/` (`Fps.Samples.Net.Editor`) |
| サンプルのテスト | `Tests/EditMode/` (`Fps.Samples.Tests`)・`Tests/EditMode/Net/` (`Fps.Samples.Net.Tests`) |

取り込み方: **Window > Package Manager** で FPS Kit を選び、**Samples** の **Demo Game** の **Import** を押す。`Assets/Samples/FPS Kit/<版>/Demo Game/` に置かれる。

- 取り込んだサンプルは、Editor を開いたときに自分のフォルダを出力先ルートの既定にする (`Fps.Editor.DemoSample`)。ビルダーと smoke は、`-fpsKitRoot` を付けなければサンプルのフォルダの下に書く。
- 取り込んだら **Fps > Build Main Menu Scene** を一度実行する。メインメニューのシーンを作り、メニューから移るシーンのルート (`MenuSceneSettings` の Scene Root) を取り込んだ場所に書き直す。
- このリポジトリでは、`Assets/Samples/FpsKitDemo` が `Assets/game/Samples~/Demo` へのシンボリックリンクになっていて、取り込んだのと同じ形で開ける。サンプルを直すときは `Assets/game/Samples~/Demo` の下を直す。

## Unity の起動コマンド

以下のコマンドは、Editor を閉じた状態で、`UNITY` を Unity の実行ファイルにして、プロジェクトのフォルダで実行する。

```sh
# macOS
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
# Linux
UNITY="$HOME/Unity/Hub/Editor/6000.6.0f1/Editor/Unity"
# Windows (Git Bash)
UNITY="/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe"
```

同じプロジェクトを Editor で開いたままだと batchmode は起動できずに失敗する。

## 出力先ルート (`-fpsKitRoot`)

シーンやアセットを作るスクリプト (`Fps/Build ...` のメニュー、JSON のインポーター、smoke) は、作ったものを **出力先ルート** の下に書く。

| 生成物 | 置き場所 |
| --- | --- |
| 武器・カタログ・ロードアウト | `<root>/Weapons/` |
| モードのルール | `<root>/Rules/` |
| シーン | `<root>/Scenes/<名前>.unity` |
| ミッション | `<root>/Missions/` |
| キャラクター | `<root>/Characters/` |

出力先ルートは次の順に決まる。

1. batchmode の引数 `-fpsKitRoot <path>` (例: `-fpsKitRoot Assets/FpsKit`)
2. このプロジェクトの EditorPrefs に覚えた値 (Editor のスクリプトから `Fps.Editor.FpsKitRoot.SetPreferredOutput("Assets/FpsKit")` で設定、`Fps.Editor.FpsKitRoot.ClearPreferredOutput()` で既定に戻す)
3. 既定の出力先 (`Fps.Editor.FpsKitRoot.DefaultOutput`)。サンプルを取り込んでいればそのフォルダ、無ければ `Assets/game`

パッケージの中は書き込めないので、値は `Assets/` の下のフォルダに限る。`Assets` そのもの、`Packages/...`、`..` を含むパス、`-fpsKitRoot` の値が無い・2 度ある、のどれかなら `ArgumentException` で止まる。
ゲームのキャラクターのリグ (`Fps/Build Character Rig`) だけは、実行時の既定として本体が読むので、出力先ルートではなくパッケージの中 (`Characters/`) に書く。

## テスト (EditMode)

パッケージのテストは、manifest の `testables` に名前を足したときだけプロジェクトに読み込まれる。`Packages/manifest.json` に足す:

```json
{
  "dependencies": {
    "com.omnistove.fps": "https://github.com/omni-stove/fps.git?path=/Assets/game#v0.1.0",
    "com.unity.test-framework": "1.8.0"
  },
  "testables": [
    "com.omnistove.fps"
  ]
}
```

`com.unity.test-framework` は新しいプロジェクトなら最初から入っている。

batchmode で回す (`-runTests` は自分で Editor を閉じるので `-quit` は付けない):

```sh
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode \
  -assemblyNames "Fps.Core.Tests;Fps.Tests.EditMode;Fps.Samples.Tests" \
  -testResults "$PWD/fps-editmode-results.xml" -logFile -
```

- 終了コードが 0 ならすべて通った。0 以外なら失敗で、どのテストが落ちたかは `fps-editmode-results.xml` (NUnit 形式) にある。
- `Fps.Samples.Tests` はサンプルを取り込んだときだけある (サンプルのテストは `testables` に関係なく、取り込むと読み込まれる)。
- サンプルのネット対戦のテストは `Fps.Samples.Net.Tests`。`-assemblyNames` を省くと、プロジェクトにあるすべての EditMode テストが回る。
- テストは `Assets/__<テスト名>_<GUID>` のような一時フォルダにアセットを作り、終わると消す。

Editor からは **Window > General > Test Runner** の EditMode タブで同じテストを回せる。

## ゲームの中身を差し替える (入口のアセット)

キットは、ゲームごとに変える中身を 1 つのアセット `Fps.Runtime.GameContent` (入口) から引く。ゲームは入口を自分のものにすれば、
シーンを組み直さずに武器・ルール・モードの値・文言・見た目と音が変わる。

### 新しいゲームで差し替える手順

1. 自分のゲームのフォルダに入口を作る。どれでも同じものができる。
   - Editor の **Fps > Create Game Content** (出力先ルートの `Resources/FpsKit/GameContent.asset` に作る)
   - batchmode:
     ```sh
     "$UNITY" -batchmode -quit -projectPath . \
       -executeMethod Fps.Editor.GameContentAssets.Create \
       -gameContent Assets/MyGame/Resources/FpsKit/GameContent.asset \
       -logFile -
     ```
     `-gameContent` を省くと `<出力先ルート>/Resources/FpsKit/GameContent.asset`。値が無い・2 度あるなら例外で終了コード 1。
   - **Assets > Create > Fps > Game Content** で作り、どこかの `Resources/FpsKit/` の下に置く
2. 入口の欄に自分のアセットを入れる。`Fps.Editor.GameContentAssets.Create` は、空の欄のうち `Assets/` の下にその型のアセットがちょうど 1 つあるものを埋め
   (`[GameContentAssets] Rules = '...'`)、0 か 2 つ以上あるものは空のまま名前をログに出す (`... left empty: ...`)。もう一度呼ぶと、埋まっている欄は変えずに空の欄だけを埋め直す。
   武器・ルールのアセットは [データの差し込み](#データの差し込み) の JSON の取り込みで作れる。
3. Play を押すと、ログに `[GameContent] using '<名前>' from Resources/FpsKit/` が出る。続けて [差し替え漏れの点検](#差し替え漏れの点検) を回す。

入口は、どこかの Resources フォルダの `FpsKit/` に置いた `GameContent` 型のアセット (名前は問わない) を探して使う。2 つ以上あると
`[GameContent] 2 assets in Resources/FpsKit/ (...)` のエラーを出し、名前の順で最初のものを使う。サンプルは入口を置かないので、ゲームの入口とぶつからない。
起動のコードから別の入口に替えるなら、シーンを読む前に `GameContent.Use(content)` を呼ぶ (`null` で Resources のものに戻る)。替えると文言の表と調整値の JSON は読み直す。

### 入口の欄と引く順

どの欄も、上から順に見て最初に見つかったものを使う。シーンのコンポーネントに配線した参照は、そのシーンだけ変えたいときのためのもので、入口より先に使う。
入口に頼るシーンでは、その参照を空にしておく。

| 入口の欄 | 使う所 | 入口より先に使うもの | 入口が空のとき |
| --- | --- | --- | --- |
| `Rules` | `GameBootstrap`・`CampaignRunner` (難易度を選ばないとき)・`NetTeamDeathmatch` | シーンの `RulesDefinition`・`Rules` | `GameRules.Default` (警告) |
| `Motor` | プレイヤーの移動 (`PlayerController`・`NetworkPlayerMotor`) | コンポーネントの `MotorSettings` | `Resources/Movement/MotorSettings`、無ければ `MotorSpec.Default` |
| `Enemies` | `EnemyAgent` の判断。Inspector の種類 (`Archetype`) と同じ種類の最初のもの | `EnemyAgent` の `Definition` | `EnemyAgent` の Inspector の値 |
| `GrenadeTuning`・`PingTuning`・`KnockbackTuning` (JSON) | [グレネード・印・吹き飛ばしの調整値](#グレネード印吹き飛ばしの調整値) | — | `Resources/Tuning/*.json`、無ければ既定値 |
| `WeaponCatalog`・`Loadout` | メニューのロードアウト選択 (`MainMenuScreen`)・ネット対戦 (`NetTeamDeathmatch`)。オフラインのプレイヤーの武器は `Loadout` のスロットの武器 | `MainMenuScreen`・`NetTeamDeathmatch` の `Catalog`・`Loadout`、`GameBootstrap` の `WeaponDefinitions` | 選べない・持たない (エラー) |
| `Missions` | メニューのキャンペーン | `MainMenuScreen` の `Missions` | キャンペーンを開けない |
| `TeamDeathmatch`・`Domination`・`Hardpoint`・`KillConfirmed`・`SearchAndDestroy`・`Horde`・`GunGameLadder` | 各モードの `*Runner` | Runner の `Definition` (`GunGameRunner` は `Ladder`) | Runner の Inspector の値 (`GunGameRunner` は始められない) |
| `CharacterRig` | プレイヤー・敵の人型のリグ (`CharacterAnimator`) | `CharacterAnimator` の `RigPrefab` | `Resources/Characters/CharacterRig` |
| `Texts` (言語コードと表の並び) | 文言 (`Localizer`・`GameText`) | — | `Resources/Localization/<言語>/` の表だけ |
| `MenuScenes` | メニューから移るシーン (`MenuNavigator`) | — | `Resources/Menu/MenuSceneSettings`、無ければ既定の名前 |
| `Presentation` ほか見た目・音のカタログ | [見た目と音を差し替える](#見た目と音を差し替える) | — | `Resources/Presentation/*` |

- `Texts` の表は `Resources/Localization/<言語>/` の表の後に重ねるので、同じキーは入口の文が勝つ。表の形は [言語を足す・文を変える](#言語を足す文を変える) と同じ。言語コードは言語の一覧 (`Localization/Languages`) の `code`。
- 入口は Play の開始時に探し直す。Edit Mode で入口を置き換えたときは、スクリプトの再読み込みか Play で読み直される。

## データの差し込み

### 武器を JSON から取り込む

1 ファイルが武器 1 つ。`id`・`nameKey`・`category`・`shotNoiseRadius` のほかは、部品ごとの入れ子のオブジェクトに書く。部品の項目名は `Fps.Core` の部品の Spec (`TriggerSpec`・`AmmoSpec`・`RoundSpec`・`ImpactSpec`・`AimSpec`・`AmmoTypeSpec`) のコンストラクタ引数と同じ。例はサンプルの `Weapons/Json/` にある (`Rifle.json`・`Handgun.json`・`Launcher.json`・`Minigun.json`・`Shotgun.json`、飛ぶ弾は `Crossbow.json`、熱で撃つのは `EnergyRifle.json`、弾薬の種類は `Dmr.json`、貫通する弾は `AntiMaterial.json`)。

```json
{
  "id": "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83",
  "nameKey": "weapon.rifle",
  "category": "AssaultRifle",
  "shotNoiseRadius": 50,
  "trigger": { "fireMode": "Auto", "fireInterval": 0.1 },
  "ammo": { "kind": "Magazine", "magazineSize": 30, "reserveAmmo": 90, "reloadSeconds": 2 },
  "round": { "delivery": "Hitscan", "range": 100 },
  "impact": { "damage": 25, "falloff": { "startDistance": 20, "endDistance": 60, "minMultiplier": 0.5 } },
  "aim": { "hipSpreadDegrees": 3, "adsSpreadDegrees": 0.5, "zoomLevels": [45] }
}
```

| 部品 | 項目 | 省略できる入れ子 |
| --- | --- | --- |
| `trigger` (撃ち方) | `fireMode` (`Auto`/`Semi`)・`fireInterval` | `burst` (`roundsPerBurst`・`roundIntervalSeconds`)・`charge` (`chargeSeconds`・`minChargeFraction`・`fullChargeDamageMultiplier`・`firesWhenFull`・`firesBelowMin`・`fullChargeSpeedMultiplier`)・`spinUp` (`spinUpSeconds`・`startIntervalMultiplier`・`spinDownSeconds`)・`cycle` (`cycleSeconds`・`leavesAds`) |
| `ammo` (弾薬) | `kind` (`Magazine`/`Heat`)・`magazineSize`・`reserveAmmo`・`reloadSeconds`・`activeReloadWindowStart`・`activeReloadWindowLength`・`activeReloadFailSeconds`・`reloadStyle` (`Whole`/`PerRound`)・`reloadSecondsPerRound` | `heat` (`heatPerShot`・`coolPerSecond`・`coolDelaySeconds`・`overheatSeconds`。`kind` が `Heat` のときは必須) |
| `round` (弾) | `delivery` (`Hitscan`/`Projectile`/`Beam`/`Spray`)・`range`・`pelletCount`・`pelletSpreadDegrees`・`radius` | `penetration` (`maxPenetrations`・`damageMultiplierPerPenetration`・`maxThickness`・`materials`)・`ricochet`・`projectile` (`speed`・`gravityMultiplier`・`lifetimeSeconds`・`onWorldHit`・`visualId`)・`homing`・`beam` (`tickSeconds`・`visualId`)・`spray` (`tickSeconds`・`coneDegrees`・`visualId`)。`projectile`・`beam`・`spray` はその `delivery` のときだけ書き、そのときは必須 |
| `impact` (着弾) | `damage`・`falloff`・`structureRadius` | `explosion` (`maxDamage`・`radius`・`fullDamageRadius`・`structureDamageAtCenter`・`detonation`)・`statusEffect` (`kind`・`durationSeconds`・`tickSeconds`・`damagePerTick`・`visualId`)・`areaEffect` (`radius`・`durationSeconds`・`tickSeconds`・`damagePerTick`・`statusEffect`・`visualId`) |
| `aim` (照準) | `hipSpreadDegrees`・`adsSpreadDegrees`・`moveSpreadDegrees`・`recoilPitchDegrees`・`recoilYawDegrees`・`recoilRecoveryPerSecond`・`recoilRecoveryDelaySeconds`・`blindFireSpreadDegrees`・`zoomLevels` (覗き込みの視野角の並び。先頭が覗き込み、続きがスコープの倍率)・`swayDegrees` | `breathHold` (`maxHoldSeconds`・`recoverSeconds`・`swayMultiplier`) |
| `ammoTypes` (弾薬の種類の並び) | 各要素に `id`・`nameKey` | `round`・`impact` (書いた部品だけ武器のものを上書きする) |

- 必須は `id` と `category` だけ。部品や項目を書かなければ部品の既定 (Auto・弾倉制・ヒットスキャン 1 本・着弾でダメージ) になる。入れ子の中の項目は、`visualId` などの既定のあるもの以外は必須。
- 列挙 (`category`・`fireMode` など) は列挙子の名前の文字列。
- `spinUp` は撃つ速さを銃身の回転に比例させる。押してから `spinUpSeconds` は撃たず、`fireInterval` の `startIntervalMultiplier` 倍の間隔で撃ち始め、`spinUpSeconds` × `startIntervalMultiplier` 秒押し続けたところで `fireInterval` の間隔になる。離すと満回転から `spinDownSeconds` 秒で止まり、回転が撃ち始めの速さより上のうちに押し直せば待たずに撃てる。
- `pelletCount` 本の弾を、腰だめ・ADS・移動の散らばりを掛けた向きを中心に半角 `pelletSpreadDegrees` の円錐へ出す。向きは撃った人・発射番号・弾の番号で決まり、オフラインとサーバーで同じになる。`damage` は 1 本あたりで、1 回の射撃で同じ相手に当たった弾はヒットマーカーと被弾の知らせを 1 つにまとめる。
- `kind` が `Heat` の武器は弾倉を減らさずリロードもしない。撃つたびに熱が `heatPerShot` 上がり、最後に撃ってから `coolDelaySeconds` 経つと毎秒 `coolPerSecond` 下がる。熱が 1 に達すると `overheatSeconds` の間は熱が 1 のまま撃てず、その後 0 まで冷め切ると撃てる。HUD は弾数の代わりに熱を出し、弾薬箱からは弾を取らない。
- `penetration` の弾は当たった物を `maxPenetrations` 枚まで抜け、1 枚抜けるたびにダメージへ `damageMultiplierPerPenetration` を掛ける。抜けられるのは厚さ `maxThickness` (m) までの物で、人は厚さ 0 として数える。`materials` は面の素材 (`SurfaceMaterial` の Id) ごとの抜けやすさの並び (`[{ "materialId": "metal", "penetrability": 0.5 }]`) で、`maxThickness` に `penetrability` を掛けた厚さまで抜ける。`penetrability` が 0 の素材は抜けずに止まり、並べていない素材は 1。ネット対戦のサーバーは `.fpsmap` が素材を持たないので、どの面も `default` で決める。貫通は `radius` が 0 の細い弾だけに効き、貫通する弾は `ricochet` を書いても跳ねない。
- `falloff` は `{ "startDistance": …, "endDistance": …, "minMultiplier": … }` か、減衰しないなら `"None"`。
- `nameKey` は画面に出す武器の名前を引く表のキー。文は `Resources/Localization/<言語>/` の JSON (例: `items.json` に `"weapon.rifle": "Rifle"`) に書く。表に無いキーはキーそのものが出る。空か `null` なら `id` が出る。弾薬の種類の `nameKey` も同じ (例: `ammo.dmr.armorPiercing`)。
- 部品に分ける前の平らな形 (`fireMode`・`magazineSize`・`damage`・`range`・`falloff`・`explosionRadius`・`adsFieldOfView` などを武器の直下に書く) もそのまま読める。`explosionRadius` は `impact.structureRadius`、`adsFieldOfView` は `aim.zoomLevels` の 1 つ目として読み、`adsFieldOfView` は `null` (共通の初期値) も書ける。同じ値を平らな項目と部品の両方に書くとエラーになる。
- 弾薬の種類を持つ武器は並びの先頭の種類で始まり、SwitchAmmoType (キーボードは N) で次の種類へ順に切り替える (最後の次は先頭)。弾倉は種類で共通で、切り替えると込めてあった弾を予備弾に戻すので、新しい種類で撃つにはリロードが要る。HUD には今の種類の名前が出る。ネット対戦ではサーバーが同じ選び方をし、射撃の要求の `AmmoTypeIndex` (0 は武器そのものの弾、種類 n は n + 1) がサーバーの選択と違えばサーバーの選択で撃つ。
- 前の項目名 `displayName` は読まない (書き換えを促すエラーになる)。知らない項目はエラーになる。
- 飛ぶ弾・ビーム・火炎・爆発の見た目と音は JSON には書かず、取り込んだ `WeaponDefinition` の各部品の参照 (`Visual`・`Sound`) に Inspector で入れる。空ならコードで形を作る (飛ぶ弾は細い棒)。刺さった弾を残す秒数 (`StuckSeconds`) も見た目だけの値なので同じ所に置く。取り込み直しても参照は消えない。
- `statusEffect` の `kind` は状態異常の種類の Id (省略すると `burn`)。同じ種類をかけ直すと重ねずに長い方へ延ばし、ダメージはかけた人と武器に帰属する。かかっている体の見た目は種類の Id ごとに `Resources/Presentation/StatusEffectVisualsCatalog.asset` (`StatusEffectVisualCatalog`) に入れ、無ければ `burn` だけコードで炎を作る。自分にかかっている間の HUD の文は `hud.json` の `hud.status.<種類の Id>`。
- サンプルでは **Fps > Build Weapon Assets** (`Fps.Editor.WeaponAssetBuilder.Build`) が `Weapons/Json/` の JSON をすべてアセットにし、射撃場で拾える武器の一覧 (`Weapons/RangeWeapons.asset`) に並べる。武器を足すときは JSON を 1 つ置いて走らせ直し、TPS の射撃場を作り直す。

batchmode で取り込む:

```sh
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Fps.Editor.WeaponJsonImporter.Import \
  -weaponJson "Assets/Samples/FPS Kit/0.1.0/Demo Game/Weapons/Json" \
  -weaponOut Assets/FpsKit/Weapons \
  -logFile -
```

| 引数 | 必須 | 意味 |
| --- | --- | --- |
| `-weaponJson <file\|dir>` | 必須 | JSON 1 つか、直下の `*.json` を名前の順に読むフォルダ。プロジェクトからの相対か絶対パス。`Packages/...` もそのまま渡せる |
| `-weaponOut <dir>` | 省略可 | アセットを置く `Assets/` の下のフォルダ。省くと `<出力先ルート>/Weapons` (`-fpsKitRoot` が効く) |
| `-weaponCatalog <asset>` | 省略可 | 取り込んだ武器を Id で重複なく足す `WeaponCatalogAsset` のパス (例: `Assets/FpsKit/Weapons/WeaponCatalog.asset`)。そのカタログが既に無いとエラー |

- アセットの名前は JSON のファイル名 (`Rifle.json` → `Rifle.asset`)。既にあれば GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
- 成功するとログに `[WeaponJsonImporter] saved '<アセット>' (id '<Id>') from '<JSON>'` が出て、終了コード 0 で終わる。
- 値が不正・項目が欠けている・知らない項目がある・Id が重複している、などのときは、ファイル名と項目名を添えた例外がログに出て、**1 つも保存せずに** 終了コード 1 で終わる。

Editor からは **Fps > Import Weapons From JSON** でフォルダを選ぶ (出力先は `<出力先ルート>/Weapons`、カタログがあれば足すかを聞かれる)。

### モードのルールを JSON から取り込む

1 ファイルがルール 1 つ (`GameRulesDefinition`)。例はサンプルの `Rules/Json/` にある (`TeamDeathmatch`・`Domination`・`Range`・`TpsRange`・`CampaignEasy`/`Normal`/`Hard`)。

```sh
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Fps.Editor.GameRulesJsonImporter.Import \
  -rulesJson "Assets/Samples/FPS Kit/0.1.0/Demo Game/Rules/Json" \
  -rulesOut Assets/FpsKit/Rules \
  -logFile -
```

| 引数 | 必須 | 意味 |
| --- | --- | --- |
| `-rulesJson <file\|dir>` | 必須 | JSON 1 つか、直下の `*.json` を名前の順に読むフォルダ |
| `-rulesOut <dir>` | 省略可 | アセットを置く `Assets/` の下のフォルダ。省くと `<出力先ルート>/Rules` |

成功すると `[GameRulesJsonImporter] saved '...'` が出る。失敗したときの扱い (1 つも保存せず終了コード 1) は武器と同じ。
Editor からは **Fps > Import Game Rules From JSON**。

### グレネード・印・吹き飛ばしの調整値

グレネード (`GrenadeSpec`)・印 (`PingSpec`)・吹き飛ばし (`KnockbackSpec`) の値は、取り込みをせずに Play の開始時に Resources の JSON から読む (`Fps.Runtime.KitTuning`)。
オフラインの `GrenadeThrower`・`PingController` も、ネット対戦のサーバー (`NetTeamDeathmatch.GrenadeSpec`・`PingSpec`) も同じ値を使う。
吹き飛ばしはプレイヤーの `KnockbackMovement` と敵の `EnemyAgent` が使い、重さはプレイヤーが `playerWeight`、敵が `enemyWeight` を読む。

| Resources のパス | 項目 (名前は Spec のコンストラクターの引数名。省いた項目は既定値) |
| --- | --- |
| `Tuning/grenade.json` | 種類の並び `types` と、出撃のたびに持つ種類の Id の並び `carried` (省くと先頭の種類だけ)。1 種類は `id` (UUID)・`nameKey`・`fuseSeconds`・`throwSeconds`・`throwSpeed`・`throwLiftSpeed`・`stickiness` (`None`・`Surfaces`・`SurfacesAndCharacters`) と、信管が切れたときの効果を `impact` (武器の JSON と同じ書き方で `explosion`・`statusEffect`・`areaEffect`) か、爆発だけなら平らな `maxDamage`・`damageRadius`・`fullDamageRadius`・`structureDamageAtCenter` で書く。`types` の無い 1 つのオブジェクトは、その 1 種類だけを持つ |
| `Tuning/ping.json` | `lifetimeSeconds`・`maxMarkers`・`maxDistance`・`enemyAimToleranceDegrees`・`minIntervalSeconds` |
| `Tuning/knockback.json` | 力 (`KnockbackForce`) は種類 (`explosion`・`melee`・`charge`) を頭に付けて開く: `explosionSpeed`・`explosionUpwardRatio`・`explosionWeightFactor`・`meleeSpeed`・`meleeUpwardRatio`・`meleeWeightFactor`・`chargeSpeed`・`chargeUpwardRatio`・`chargeWeightFactor`。ほかは `minSpeed`・`minAirSeconds`・`maxAirSeconds`・`airControl`・`gravity`・`recoverSeconds`・`landingSnapRadius`・`playerWeight`・`enemyWeight` |

```json
{
  "types": [
    { "id": "ff94f232-2647-4b77-a077-5693d8f954d1", "nameKey": "grenade.frag", "damageRadius": 8, "throwSpeed": 18 },
    {
      "id": "ecaf6e57-cb77-4707-9321-6ea5b659408f",
      "nameKey": "grenade.smoke",
      "impact": { "damage": 0, "areaEffect": { "radius": 5, "durationSeconds": 15, "tickSeconds": 0.5, "damagePerTick": 0, "visualId": "smoke" } }
    }
  ],
  "carried": ["ff94f232-2647-4b77-a077-5693d8f954d1", "ecaf6e57-cb77-4707-9321-6ea5b659408f"]
}
```

- 持つ種類それぞれに、ルールの `grenadeCount` 個を持つ。2 種類以上持つと、SwitchGrenade (K) で投げる種類を切り替え、HUD に種類の名前を出す。
- 範囲効果 (`areaEffect`) は `AreaEffectZone` が出す。見た目は `Resources/Presentation/AreaEffects/<visualId>` のプレハブで、無ければ仮の球を出す。
- `damagePerTick` が 0 で `statusEffect` の無い範囲効果はスモークになる。出ている間、その球を通る敵 (オフライン) と Bot (サーバー) の視線を遮る (ログは `[Smoke] blocks sight`・`[Smoke] cleared`)。弾は通り、ダメージは無い。見た目のプレハブが無ければ、仮の球の代わりに煙の粒子を出す。

- パッケージにも同じパスの JSON (今の既定値) が入っている (`Packages/com.omnistove.fps/Resources/Tuning/`)。利用側に同じパスのファイルを置くと
  どちらが読まれるか決まらないので、ゲームの値は入口 ([ゲームの中身を差し替える](#ゲームの中身を差し替える-入口のアセット)) の `GrenadeTuning`・`PingTuning`・`KnockbackTuning` に
  JSON を入れる (入口の JSON があれば Resources の JSON は読まない。ログは `[Tuning] read GameContent '<入口>' (<JSON>)`)。
  コードからは `GrenadeThrower.ApplySpec`・`PingController.ApplySpec`・`NetTeamDeathmatch.GrenadeSpec`・`PingSpec`、`KnockbackMovement`・`EnemyAgent` の `KnockbackSpec`・`KnockbackWeight` に渡す。
- ファイルが無ければ既定値で動く。壊れている・値が不正なら `[Tuning]` 付きのエラーにファイル名と項目名を出し、既定値で続ける。
- Play 中に生まれたコンポーネントは、Inspector の値をこの JSON の値で上書きする (Play 中に Inspector で変えた値はその後に効く)。

### Inspector から変えられる値

取り込んだアセットや、ビルダーが置いたコンポーネントは、Inspector で値を変えられる。どの項目にも Tooltip が付いている。

| 対象 | 主な値 |
| --- | --- |
| `WeaponDefinition` (`<root>/Weapons/*.asset`) | 弾数・連射間隔・リロード・ダメージ・射程・距離減衰・爆発半径・ADS の視野角・ブラインドファイアの散らばり・アクティブリロードの判定・反動・弾の散らばり・銃声が敵に聞こえる距離 (消音した武器は小さくする) |
| `WeaponCatalogAsset`・`LoadoutDefinition` | 使える武器の一覧と、最初に持つ武器 |
| `GameRulesDefinition` (`<root>/Rules/*.asset`) | フレンドリーファイア・リスポーン・自動回復・死亡時の見せ方・弾薬・建造物の破壊・ダウンやカバーなどのアクション・スポーン保護・落下ダメージ・HUD の量・勝敗の決め方 |
| `GrenadeThrower` (プレイヤーのコンポーネント) | 爆発までの秒数・投げる間隔・初速・ダメージ・爆発の半径・建造物への被害 |
| `EnemyStealth` (敵のコンポーネント) | 発見ゲージの視野・たまる速さと距離・角度・姿勢・動きの係数・減り方・気づき/警戒/交戦の境目・仲間に交戦を伝える範囲・銃声と足音でゲージに足す量・物音を調べに行くときの量 (音だけではこれより上がらない)・頭上の印のプレハブ |
| `StealthTarget` (プレイヤーのコンポーネント) | 明るさの代わりの係数 (暗がりを作るゲームがここを書き換える)・伏せているか・足音が敵に聞こえる距離 |
| `DistractionThrowDefinition` (能力のアセット) | 物を投げる初速と上向きの速さ・落ちた音が敵に聞こえる距離・消すまでの秒数・投げる物の見た目と落ちた音 (空なら仮の球で、音は鳴らさない) |
| `PingController` (プレイヤーのコンポーネント) | 印が消えるまでの秒数・同時に付けられる数・照準の先を探す距離・照準が外れていても敵に付ける角度・続けて付ける間隔 |
| `SquadCommandController` (プレイヤーのコンポーネント) | 長押しとみなす秒数・照準の先を探す距離・照準が外れていても敵を指したとみなす角度・分隊の無いモードで指示が届く距離・付いてくる bot が寄ってくる距離と撃ち合いをやめて戻る距離 |
| `BossController` (ボスのコンポーネント) | 段階ごとの体力の閾値・動きの速さの倍率・出す弱点 (`BossWeakPoint` の Id)、部位ごとのダメージ倍率 (頭・胴・手足・弱点) |

JSON から取り込み直すと、そのアセットの値は JSON の値で上書きされる。Inspector で変えた値を残したいなら、JSON にも同じ値を書く。

### ボスの段階と弱点

`BossController` は体力の割合で段階を進め、段階ごとに `EnemyAgent` の `EnemySpec`・`IEnemyBehavior`・動きの速さの倍率と、出す弱点 (`BossWeakPoint`、部位は `HitZone.WeakPoint`) を差し替える。
振る舞いや、体力以外の条件 (経過時間・仕掛けが壊れたなど) で進む段階は Inspector に書けないので、コードで `Fps.Core.BossPhases` を組んで `BossController.UsePhases` に渡す。
行動の合図で弱点を出し入れするときは `SetWeakPointExposed`、条件を待たずに次へ進めるときは `Advance` を呼ぶ。

段階が変わると `CombatEvents.BossPhaseChanged` が出る (復活で最初の段階に戻ったときは `IsReset` が true)。カメラ・音・HUD の体力バーはここを購読して出す。
ログは `[Boss] id=.. phase=0->1 from=.. to=.. reason=..`、命中ごとに `[Boss] id=.. hit zone=.. damage=..`、弱点の出し入れは `[Boss] id=.. weakpoint=.. exposed=..`。
TPS の射撃場 (`Fps/Build TPS Range Scene`) の左奥に、体力の半分で弱点が出て速くなる仮のボス (`RangeBoss`) が置かれる。

## 調整値を差し替える

ゲームが変える数値は、すべてデータに出してある。コードには既定値だけがある。

| 調整値 | 置き場所 | 入口の欄 |
| --- | --- | --- |
| 武器 (弾数・連射・ダメージ・反動など) | `WeaponDefinition` (JSON から取り込む: [武器を JSON から取り込む](#武器を-json-から取り込む)) | `WeaponCatalog`・`Loadout` |
| ルール (味方撃ち・リスポーン・回復・HUD の量・勝敗など) | `GameRulesDefinition` (JSON から取り込む: [モードのルールを JSON から取り込む](#モードのルールを-json-から取り込む)) | `Rules` |
| モードの得点・制限時間・リスポーンなど | モードごとの定義 (`TeamDeathmatchDefinition`・`DominationDefinition`・`HardpointDefinition`・`KillConfirmedDefinition`・`SearchAndDestroyDefinition`・`HordeDefinition`・`GunGameLadderDefinition`)。**Assets > Create > Fps > Modes** などで作る | モード名の欄 |
| 移動 (歩き・ダッシュ・しゃがみの速さ、ジャンプ、重力、空中の操作) | `MotorSettings` (**Assets > Create > Fps > Motor Settings**) | `Motor` |
| 敵 AI (視界・反応・散らばり・遮蔽・怯み・種類ごとの値) | `EnemyDefinition` (**Assets > Create > Fps > Enemy Definition**) | `Enemies` |
| グレネード・印・吹き飛ばし | JSON ([グレネード・印・吹き飛ばしの調整値](#グレネード印吹き飛ばしの調整値)) | `GrenadeTuning`・`PingTuning`・`KnockbackTuning` |
| メニューから移るシーン | `MenuSceneSettings` (**Assets > Create > Fps > Menu Scene Settings**) | `MenuScenes` |
| そのほか (ステルス・印・分隊・ボスなど) | コンポーネントの Inspector ([Inspector から変えられる値](#inspector-から変えられる値)) | — |

## 見た目と音を差し替える

キットの見た目と音は、差し替えられる参照 (カタログのアセットかコンポーネントのプレハブの欄) から引く。参照が空のときだけ、コードで仮の形 (カプセル・箱・球) を作るか、出さずに進む。

| カタログ (型) | 中身 | 入口の欄 | 入口が空のときの Resources のパス |
| --- | --- | --- | --- |
| `PresentationCatalog` | 武器 Id ごとの発射音・マズルフラッシュ・着弾、ヒットマーカーとキルマーカーの音 | `Presentation` | `Presentation/PresentationCatalog` |
| `ViewmodelCatalog` | 武器 Id ごとの一人称の銃のモデル | `Viewmodels` | `Presentation/ViewmodelCatalog` |
| `WeaponExtrasCatalog` | 武器 Id ごとの薬莢と空撃ちの音 | `WeaponExtras` | `Presentation/WeaponExtrasCatalog` |
| `HitFeedbackCatalog` | 部位ごとの血しぶき・背後の壁の血痕 | `HitFeedback` | `Presentation/HitFeedbackCatalog` |
| `DamageFeedbackCatalog` | 被弾と低体力のときに画面に重ねる赤と、低体力の心音・息の音 | `DamageFeedback` | `Presentation/DamageFeedbackCatalog` |
| `SurfaceImpactCatalog` | 材質ごとの銃痕と着弾 | `SurfaceImpacts` | `Presentation/SurfaceImpactCatalog` |
| `FootstepSurfaceCatalog` | 材質ごとの足音 | `Footsteps` | `Presentation/FootstepSurfaceCatalog` |
| `RagdollCatalog` | やられたときのラグドールの骨 | `Ragdoll` | `Presentation/RagdollCatalog` |
| `SuppressionCatalog` | 制圧されたときの画面の色 | `Suppression` | `Presentation/SuppressionCatalog` |
| `AwarenessIndicatorCatalog` | 発見ゲージの頭上の印のプレハブ | `AwarenessIndicator` | `Presentation/AwarenessIndicatorCatalog` |
| `CameraShakeSettings` | 撃ったときなどのカメラの揺れ | `CameraShake` | `Presentation/CameraShakeSettings` |
| 人型のリグ (プレハブ) | プレイヤー・敵の体とアニメーション | `CharacterRig` | `Characters/CharacterRig` |

- カタログは **Assets > Create > Fps > <カタログ名>** で作り、武器の Id や材質の Id で引く (武器の名前では引かない)。サンプルの仮の素材は `Fps/Build All Presentation Assets` (`Fps.Editor.PresentationAssetSteps.BuildAll`) で作れる。
- 1 つの物だけの見た目は、そのコンポーネントのプレハブの欄で変える (例: `GameBootstrap` の床・標的ダミー・グレネード、`EnemySniper`・`EnemyBomber`・`EnemyShield` の仮の形、`Pickup`・`PickupSpawnPoint`・`BombSite`・`ControlPointZone` の見た目、`TurretDeployDefinition`・`DecoyDeployDefinition`・`MineDeployDefinition`・`DeployAbilityDefinition` の置く物)。空なら仮の形を作る。
- 画面 (HUD・メニュー) は [UI を差し替える](#ui-を差し替える) で替える。
- 武器の Id にカタログの音とエフェクトが無いと、[差し替え漏れの点検](#差し替え漏れの点検) が一覧に出す。

## UI を差し替える

ゲーム進行 (`Fps.Runtime`) は、画面を具体的なクラスでは持たず、登録口 `Fps.Runtime.GameUi` に入っている実装を使う。
既定の UI (`Fps.Runtime.UI`) は起動時に自分を **既定** として登録するだけなので、使う側が登録口のプロパティに自分の実装を入れれば、既定の UI のコードは呼ばれなくなる。
ネット対戦の HUD (`Fps.NetV2` の `NetV2Hud`) は、オフラインと同じ HUD (`Hud`・`Hud.uxml`) を使う。

| 登録口のプロパティ | 実装するインターフェース | 使われる所 | 既定 |
| --- | --- | --- | --- |
| `GameUi.HudFactory` | `IHudFactory` (作るのは `IHud`) | オフラインの HUD とプレイヤーのデバッグ表示 (`GameBootstrap` が作る) | `Fps.Runtime.UI` の HUD。ポーズメニュー・ロードアウトの選択・字幕なども、この HUD の GameObject に載っている |
| `GameUi.Pause` | `IPauseState` | ポーズ画面が開いているか (開いている間は操作を止める) | 既定の HUD のポーズメニュー |
| `GameUi.LoadoutSelect` | `ILoadoutSelect` | カーソルを解放してロードアウトの画面を開いているか | 既定の HUD のロードアウトの画面 |
| `GameUi.Menu` | `IMenuNavigator` | メインメニューへ戻る・メニューで選んだチュートリアルを受け取る | `Fps.Runtime.UI` のメニューのシーンとの行き来 |
| `PingUi.ViewFactory` | `IPingViewFactory` (作るのは `IPingView`) | オフラインのプレイヤーが付けた印 (`PingController`) を画面に出す。画面の外の印は端に寄せて向きを出す | 既定の HUD (`Hud`) に付く `PingMarkersView`。ほかの HUD には出さない (`null` を返す) |
| `SquadOrderUi.ViewFactory` | `ISquadOrderViewFactory` (作るのは `ISquadOrderView`) | プレイヤーが味方の bot に出した指示 (`SquadCommandController`) の守る場所・狙う敵を画面に出す | 既定の HUD (`Hud`) に付く `SquadOrderMarkerView`。ほかの HUD には出さない (`null` を返す) |

- 入れた実装は、登録の順番に依らず既定より優先される。`null` を入れると既定に戻る。
- 既定も自分の実装も無いときは、例外にせず HUD 無しで進む (ログに `no HUD factory is registered` などが出る)。生成役が `null` を返したときだけ `InvalidOperationException` になる。
- ネット対戦の HUD は `GameUi.HudFactory` を通らない (`NetV2Hud` が既定の `Hud` を作る)。
- 既定の HUD を差し替えると、その HUD に載っていたポーズメニューやロードアウトの画面も出なくなる。自分でポーズ画面を作るなら `GameUi.Pause` にも、その開閉を返す実装を入れる (入れないと既定の `IsAnyOpen` は常に false で、ポーズ中も操作が止まらない)。
- `IHud` の `Scoreboard`・`HitMarker`・`ModeHud`・`TutorialPrompt`・`InteractPrompt`・`AttachDomination()` は、持っていなければ `null` を返してよい。ゲーム進行はその表示を出さずに進む。`AttachGrenadeCount()` と `AttachAbilitySlots()` は `null` を返さない (プレイヤーがグレネードや能力の枠を持つと、返した値の `Show` をすぐ呼ぶ)。
- 登録はシーンを読む前に済ませる。`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` を付けた static メソッドで入れるのが簡単。
- 発見ゲージの頭上の印 (ルールの `stealth` が On のときだけ出る) は、根に `IAwarenessIndicator` を実装したコンポーネントを持つプレハブを、どこかの Resources フォルダの `Presentation/AwarenessIndicatorCatalog.asset` (`AwarenessIndicatorCatalog`、入口の `AwarenessIndicator` が先) に入れると差し替わる。敵ごとに変えるなら `EnemyStealth` の印のプレハブに入れる。どちらも空なら仮の球を出す。

### 最小の例: オフラインの HUD

利用側のプロジェクトの `Assets/` の下 (例: `Assets/MyGame/MyHud.cs`) に置く。`Fps.Core`・`Fps.Runtime` は `autoReferenced` なので、asmdef を使わないスクリプト (Assembly-CSharp) からはそのまま参照できる。自分の asmdef に置くなら `references` に `Fps.Core` と `Fps.Runtime` を足す。

```csharp
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class MyHud : MonoBehaviour, IHud, IGrenadeCount, IAbilitySlots
{
    private Damageable _player;
    private WeaponController _weapon;
    private GrenadeThrower _grenades;

    public static MyHud Create(string objectName, Damageable player, WeaponController weapon)
    {
        var hud = new GameObject(objectName).AddComponent<MyHud>();
        hud._player = player;
        hud._weapon = weapon;
        return hud;
    }

    public PanelSettings OwnedPanelSettings => null;
    public IScoreboard Scoreboard => null;
    public IHitMarker HitMarker => null;
    public IModeHud ModeHud => null;
    public ITutorialPrompt TutorialPrompt => null;
    public IInteractPrompt InteractPrompt => null;

    public void ApplyDensity(HudDensity density) { }
    public void BindMatch(Match match) { }
    public void BindNotice(CampaignNotice notice) { }
    public void UseTeamPalette(Team? viewer) { }
    public void SetPauseTimeMode(PauseTimeMode mode) { }
    public IGrenadeCount AttachGrenadeCount() => this;
    public IAbilitySlots AttachAbilitySlots() => this;
    public IDominationHud AttachDomination() => null;

    public void Show(GrenadeThrower thrower) => _grenades = thrower;
    public void Show(AbilityHost host) { }

    private void OnGUI()
    {
        GUILayout.Label("MY HUD");
        if (_player != null && _player.Health != null)
        {
            GUILayout.Label($"HP {_player.Health.Current} / {_player.Health.Max}");
        }

        if (_grenades != null && _grenades.UsesGrenades)
        {
            // 既定の HUD と同じ表のキーを引けば、言語の切り替えと文の上書きがそのまま効く
            GUILayout.Label(Localizer.Format("hud.grenades", _grenades.Remaining));
        }
    }
}

public sealed class MyHudFactory : IHudFactory
{
    public IHud Create(string objectName, Damageable player, WeaponController weapon, Match match)
    {
        return MyHud.Create(objectName, player, weapon);
    }

    public void AttachActionDebug(GameObject player, PlayerActionReader reader) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameUi.HudFactory = new MyHudFactory();
    }
}
```

`IHud.gameObject` は `MonoBehaviour` の `gameObject` がそのまま実装になる。ゲーム進行はシーンを片付けるときにこの GameObject (と `OwnedPanelSettings`) を消す。

確かめ方:

1. **Fps > Build Weapon Assets**・**Fps > Build Game Rules Assets**・**Fps > Build Bootstrap Scene** を順に実行し、`<出力先ルート>/Scenes/Bootstrap.unity` を開いて Play に入る。
2. 上の例を置く前は既定の HUD (体力・弾数・照準) が出る。置いてから Play に入り直すと、既定の HUD は出ずに左上に `MY HUD` と HP が出る。
3. `Register` の中を `GameUi.HudFactory = null;` にすると既定の HUD に戻る。

### ポーズ・ロードアウト・メニューの様子

自分のポーズ画面を作ったら、開閉を返す実装を入れる。`GameUi.LoadoutSelect` (`ILoadoutSelect.IsAnyOpen`) も同じ形。

```csharp
public sealed class MyPauseState : IPauseState
{
    public bool IsAnyOpen => MyPauseScreen.IsOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GameUi.Pause = new MyPauseState();
    }
}
```

`GameUi.Menu` (`IMenuNavigator`) は、既定のメインメニューのシーンを使わないときに差し替える。`ReturnToMenu` はメニューのシーンを読めなければ `false` を返して今のシーンに留まり、`ReturningToMenu` はシーンをまたいで残るもの (NetworkManager など) を片付けるために、メニューのシーンを読む直前に呼ぶ。

### 印 (ピン) の表示

`PingController` は、ルールの `Ping` が On の間、`GameUi.FindHud()` で見つけた HUD を `PingUi.ViewFactory` に渡して表示を受け取り、`IPingView.Show` で自分を渡す。
表示は `PingController.Markers` (古い順の `PingMarker`) を毎フレーム読み、画面の外の位置と向きは `Fps.Core.PingScreen.Place` で決められる。
自分の HUD に出すなら、HUD に合わせた `IPingViewFactory` を入れる (既定の HUD のまま印の見た目だけ変えるなら、`Hud.uss` の `.ping-marker` の規則を上書きする)。

```csharp
public sealed class MyPingViewFactory : IPingViewFactory
{
    public IPingView Create(IHud hud)
    {
        var view = hud.gameObject.GetComponent<MyPingView>();
        return view != null ? view : hud.gameObject.AddComponent<MyPingView>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        PingUi.ViewFactory = new MyPingViewFactory();
    }
}
```

### 分隊への指示

プレイヤーの `SquadCommandController` が SquadCommand (キーボードは B) の入力を読み、同じ陣営 (`SideMember`) の味方の bot (`EnemyAgent`) に指示を出す。

| 押し方 | 照準の先 | bot がすること |
| --- | --- | --- |
| 短く押して離す | 地面や物 | そこへ行って守る (`EnemyAgent.OrderHold`)。撃たれたり敵が見えたりすれば反撃するが、追って離れない |
| 短く押して離す | 敵 | その敵を狙う (`EnemyAgent.OrderAttack`)。見えなければ居場所へ向かい、倒したら付いてくるのに戻る |
| 長押し | — | 指示を取り消して付いてくる (`EnemyAgent.OrderFollow`) |

- 指示は bot 自身の判断やモードの用事 (`EnemyAgent.SetErrand`) より優先し、合わせ方は `Fps.Core.BotDriver.FollowOrder` が決める。
- 分隊のあるモード (`ModeRunnerBase.HasSquads` が true。既定ではバトルロイヤル) では分隊の全員に、ほかでは近くの同じ陣営の bot にだけ出す。
- 指示の印は `SquadOrderUi.ViewFactory` の表示 (既定は `SquadOrderMarkerView`、見た目は `Hud.uss` の `.squad-order__*`) に出る。
- bot の応答は字幕 (`SubtitleFeed.Say`) に出る。文は表 (`Resources/Localization/<言語>/squad.json` の `squad.reply.*`) から引く。
- ログは `[Squad] order` / `refused` (指示した側)、`[Squad] accepted` / `arrived` / `engage` / `done` (bot)。
- TPS の射撃場 (`Fps/Build TPS Range Scene`) の右の中ほどに、味方の bot (`SquadCommandYard/SquadBot`) が置かれる。プレイヤーに同じ陣営 (0) を持たせると指示を出せる。

### フォトモード

既定の HUD のポーズ画面の「フォトモード」で入る (`PhotoModeView`、HUD の `PhotoMode`)。時間はポーズ画面の扱いのまま (オフラインは止まり、オンラインは進む) で、HUD を隠し、自分のカメラだけをプレイヤーの目から `PhotoModeSpec.Radius` の中で自由に動かす。

| 操作 | アクション |
| --- | --- |
| 動かす・向きを変える | Move・Look |
| 撮る | Attack |
| 寄る・引く (視野角) | Next・Previous |
| 左・右に傾ける | Cover・Interact |
| 視野角と傾きを戻す | Reload |
| 抜ける (ポーズも閉じる) | Pause |

撮った画像は既定で `Application.persistentDataPath/Photos/` に PNG で出し、`[PhotoMode] saved <パス>` のログを出す。フィルターや枠を付ける・保存先を変えるときは `IPhotoCapture` を実装して `PhotoModeView.PhotoCapture` に入れる。範囲・速さ・視野角・傾きの幅は `PhotoModeSpec` を作って `PhotoModeView.Spec` に入れる。

```csharp
var photo = hud.PhotoMode;
photo.Spec = new PhotoModeSpec(radius: 12f, moveSpeed: 4f, minFieldOfView: 15f, maxFieldOfView: 90f,
    fieldOfViewStep: 5f, maxRoll: 30f, rollSpeed: 30f, startHeight: 0.6f, startBackDistance: 2.5f);
photo.PhotoCapture = new MyFramedCapture();
```

## 言語を足す・文を変える

画面に出す文は、言語ごとの表 (キーから文への JSON) から引く。表は Resources の次の場所から読み、前から順に重ねて、同じキーは後のものが勝つ。

1. `Resources/Strings/<言語コード>.json` (パッケージの `Localization/Resources/Strings/`。まだ移していない古いキー)
2. `Resources/Localization/<言語コード>/` にあるすべての JSON を、ファイル名 (拡張子なし) の順に。順は大文字小文字を区別する文字コード順 (`Z` < `a`)

パッケージの `Resources` も利用側の `Assets/Resources` も同じ Resources なので、**利用側の `Assets/Resources/Localization/<言語コード>/` に JSON を置けば**、パッケージの表に重なる。言語コードは `ja` (日本語) と `en` (英語)。

### 文を変える (キーを上書きする)

1. 変えたいキーを、パッケージの表から探す。キーの一覧は `Packages/com.omnistove.fps/Resources/Localization/ja/` (このリポジトリでは `Assets/game/Resources/Localization/ja/`) と `Packages/com.omnistove.fps/Localization/Resources/Strings/ja.json` にある。

   | ファイル | 中身 |
   | --- | --- |
   | `common.json` | 画面をまたいで使う語 (今は空) |
   | `hud.json` | HUD・スコアボード・キルフィード |
   | `items.json` | 武器などの名前 |
   | `menu.json` | メインメニュー・ポーズ・ロードアウトの選択 |
   | `settings.json` | 設定とキー割り当て。`settings.language.name` は言語の選択に出すその言語自身の名前 |
   | `campaign.json` | チュートリアル・キャンペーン・ミッション |
   | `modes-control.json`・`modes-survival.json` | モードごとの HUD |
   | `Strings/ja.json` | まだ上のファイルに移していないキー (モードの開始・ラウンドなど) |

2. 利用側に `Assets/Resources/Localization/ja/zz-mygame.json` を作り、変えたいキーだけを書く。

   ```json
   {
     "hud.reloading": "装填中"
   }
   ```

3. Play に入り直す。射撃場 (`Bootstrap.unity`、作り方は [最小の例](#最小の例-オフラインの-hud) の確かめ方を参照) で数発撃ってからリロードすると、`リロード中` の代わりに `装填中` が出る。

- **ファイル名は、パッケージのどのファイル名よりも後に並ぶものにする** (例: `zz-` で始める)。前に並ぶと、パッケージの表に上書きし返される。パッケージと **同じ名前** にすると、どちらが後になるかは決まらない。
- 利用側に `Resources/Strings/<言語コード>.json` を置かない。パッケージの同じパスの表とぶつかり、片方しか読まれない。
- 表は、Play の開始時と初めて引いたときに読み、Play の間は持ち続ける。JSON を変えたら Play に入り直す (`Localizer.Reload()` を呼んでも読み直す)。
- 言語は、初めて起動したときに OS の言語から選ばれる (パッケージの一覧では、日本語なら `ja`、それ以外は `en`。[言語を足す](#言語を足す) の `systemLanguages`)。`ja` の上書きを確かめるときに `en` が出ていたら、ポーズ > 設定の言語で日本語を選ぶ (選んだ言語は保存される)。
- 自分の UI で表を引くときは `Fps.Runtime.Localizer.Get(key)`・`Localizer.Format(key, 引数…)`・`Localizer.Resolve(LocalizedText)` を使う。言語が変わると `Localizer.Changed` が呼ばれるので、文を持ち続ける画面はそこで引き直す。

### 書式

- 1 ファイルが入れ子の無い JSON のオブジェクト 1 つで、値はすべて文字列。空のキー・同じファイルの中での同じキーの重なり・文字列でない値があると、そのファイルは読まれない。UTF-8 で書く (先頭の BOM は読み飛ばす)。
- `{0}`・`{1}`… に、ゲームが渡す引数が順に入る。数は言語に依らない書き方 (小数点は `.`) になる。語順に合わせて、番号の出る位置は言語ごとに変えてよい (例: `"hud.mode.remaining": "残り {1} の {0}"`)。
- 引数が何番まであるかは、同じキーの `ja` の文にある番号と同じ。渡されない番号を書くと、`{2}` のまま画面に出て、`[Localization]` の警告が出る。
- `{` と `}` そのものを出すときは `{{` と `}}` と書く。
- 引数に別のキーの文が入ることがある (例: 武器の名前)。その文も今の言語の表から引かれる。

### 表に無いキー・壊れた表

- 表に無いキーは、**キーそのもの** (例: `hud.reloading`) が画面に出て、キーごとに一度だけ `[Localization] ja: missing key '...'; showing the key itself` の警告が出る。どの文が足りないかは画面とログで分かる。
- `en` の表のどれかが壊れていると、`en` 全体を使わずに `ja` の表で続け、警告を出す。
- `ja` の表のどれかが壊れていると、壊れたファイルだけエラーを出して飛ばし、残りの表で続ける。

### 言語を足す

設定画面の言語の選択に出るのは、言語の一覧 (`Resources/Localization/Languages/` の JSON) にあり、`Resources/Localization/<言語コード>/` に表が 1 つでもある言語。並びは一覧の順。パッケージの一覧 (`Resources/Localization/Languages/languages.json`) は `ja` と `en`。

```json
{
  "otherSystemLanguages": "en",
  "languages": [
    { "code": "ja", "name": "日本語", "systemLanguages": ["Japanese"] },
    { "code": "en", "name": "English", "systemLanguages": ["English"] }
  ]
}
```

| キー | 意味 |
| --- | --- |
| `code` | 言語コード。保存する設定とフォルダー名 (`Localization/<言語コード>/`) に使う。ASCII の英字で始まり、英数字・`-`・`_` だけで 35 文字まで。大文字小文字を区別する |
| `name` | その言語自身での名前。表に `settings.language.name` が無いときに設定画面に出る |
| `systemLanguages` | 初めて起動したとき、OS の言語がこの名前 (Unity の `SystemLanguage` の名前) ならこの言語にする。省いてよい |
| `otherSystemLanguages` | OS の言語がどの `systemLanguages` にも無いときの言語。省くと一覧の最初の言語 |

言語 (例: `fr`) を足すには、コードを変えずに次の 2 つを置く。

1. 利用側に `Assets/Resources/Localization/Languages/zz-mygame.json` を作り、足す言語の行だけを書く。

   ```json
   { "languages": [ { "code": "fr", "name": "Français", "systemLanguages": ["French"] } ] }
   ```

2. `Assets/Resources/Localization/fr/` に、`ja` と同じキーの表 (`settings.json` など) を置く。

- 一覧のファイルは、表と同じくファイル名 (拡張子なし) の順に重なる。後のファイルにある言語は、前の行を同じ位置のまま置き換える (名前を変えるときに使う)。新しい言語は後ろに足される。`otherSystemLanguages` は最後に書いたファイルのものが効く。**パッケージと同じ `languages.json` という名前にしない** (どちらが読まれるかが決まらない)。
- 一覧の読めないファイルや行は `[Localization]` の警告を出して飛ばす。言語が 1 つも読めなければエラーを出し、`ja` と `en` で続ける。
- 保存した設定の言語が一覧に無い (一覧から消した) ときは、`[Input]` の警告を出して OS の言語に戻す。
- `en` の文をそろえる・直すときは、`Assets/Resources/Localization/en/` に上と同じ形で JSON を置く。`en` に無いキーは `ja` の文にはならず、キーそのものが出る。足した言語も同じで、表が壊れていると `ja` の表で続ける。

### 訳さないもの

次の文は表を通さず、言語の設定に依らずコードに書いたままの文で出る。表にキーは無く、JSON で上書きもできない。

- ログ (`Debug.Log`・警告・エラー。`[GameUi]`・`[Localization]` などのタグ付きのものを含む) と、サーバーのログ・`Logs/` に書くファイル
- 例外のメッセージ (`ArgumentException`・`InvalidOperationException` など)
- エディタ向けの文 (`Fps` のメニュー・Inspector の Tooltip・ビルダーやインポーターのダイアログとログ)
- デバッグ表示 (プレイヤーのアクションのデバッグ表示・ルールのデバッグ用の欄・F3 の回線表示)

## 動作確認 (smoke)

サンプルを取り込んだうえで、`Fps.Editor.SmokeSuite.RunAll` は、次の SmokeRun を 1 つの Editor で順に Play Mode に入れて走らせる。必要な武器・ルール・シーンは各 SmokeRun が出力先ルートに作るので、空のプロジェクトでもそのまま回せる。

1. `CampaignSmokeRun`
2. `DestructionSmokeRun`
3. `EnemyFireSmokeRun`
4. `EnemyStuckSmokeRun`
5. `WeaponSwitchSmokeRun`

Play Mode に入るので、**`-quit` を付けない** (付けるとすぐに終了コード 1 で終わる)。

```sh
"$UNITY" -batchmode -projectPath . \
  -executeMethod Fps.Editor.SmokeSuite.RunAll \
  -fpsKitRoot Assets/FpsKit \
  -logFile -
```

`-fpsKitRoot` を省くと、生成物は取り込んだサンプルのフォルダに置かれる。

### 成功と失敗の見分け方

**終了コード** で見分ける: すべて通れば 0、1 つでも失敗すれば 1。ログの最後に `[SmokeSuite]` のまとめが出る。

成功:

```
[SmokeSuite] passed:
  passed CampaignSmokeRun (exit code 0)
  passed DestructionSmokeRun (exit code 0)
  passed EnemyFireSmokeRun (exit code 0)
  passed EnemyStuckSmokeRun (exit code 0)
  passed WeaponSwitchSmokeRun (exit code 0) (exit code 0)
```

失敗:

```
[SmokeSuite] failed:
  passed CampaignSmokeRun (exit code 0)
  FAILED DestructionSmokeRun (exit code 1)
  ...
  NOT RUN WeaponSwitchSmokeRun (exit code 1)
```

- `FAILED <名前>` の行の SmokeRun が落ちた。その名前のタグ (`[DestructionSmokeRun]` など) でログを探すと理由がある。1 つが 600 秒を超えても終わらないときも、`[SmokeSuite] <名前> did not finish within 600s` を出して `FAILED` になる。
- `NOT RUN <名前>` は結果を受け取れないままスイートが終わった SmokeRun で、これも失敗に数える。

## 差し替え漏れの点検

`Fps.Editor.ReplacementAudit.Run` は、差し替えたときに抜けやすいものをまとめて洗い出し、一覧をログに出す。Play Mode には入らない。

```sh
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Fps.Editor.ReplacementAudit.Run \
  -logFile -
```

Editor からは **Fps > Audit Replacements** (一覧をログに出すだけで、Editor は閉じない)。

見るもの:

| 種類 | 漏れとして出すもの |
| --- | --- |
| `Presentation` | 入口の `Presentation` にも `Resources/Presentation/PresentationCatalog` にもカタログが無い・`Validate` に通らない。`Assets/` の下の `WeaponDefinition` の Id にカタログの音とエフェクトが無い。武器の Id が空 |
| `Strings` | `ja`・`en` のどちらかに表 (Resources と入口の `Texts`) が無い・壊れている。どちらかの言語にあってもう一方に無いキー。武器の `nameKey` が空か、どちらかの言語の表に無い |
| `Reference` | ビルドの一覧のシーン (有効なもの)・`Assets/` の下のプレハブ・アセットで、`[RequiredReference]` を付けた参照が空 (消えたアセットを指すものも含む)。ビルドの一覧のシーンが無い |

`[RequiredReference]` (`Fps.Runtime.RequiredReferenceAttribute`) は、空だと動かない参照 (空なら仮の形や既定値で続けるものは除く) に付ける印。今は `HordeRunner` の Spawner・`WaveSpawner` の Enemy Template・`MissionRunner` の Definition・`EnemyAgent` の Weapon Definition に付いている。ゲームのコンポーネントやアセットのフィールドに付けても同じく見る。

**終了コード** で見分ける: 漏れが無ければ 0、漏れがあれば 1、点検そのものが例外で止まれば 2。

```
[ReplacementAudit] failed: 1 missing replacement(s) (exit code 1)
  [Strings] en: key 'hud.reloading' is missing (ja has it in 'Resources/Localization/ja/hud')
```

## アセンブリの一覧

| アセンブリ | 中身 | 有効になる条件 |
| --- | --- | --- |
| `Fps.Core` | Unity に依存しないルールと計算 | 常に |
| `Fps.Runtime` | ゲームプレイのコンポーネントとアセットの型 | 常に |
| `Fps.Runtime.UI` | 既定の UI (HUD・メニュー・ポーズ・設定)。起動時に `GameUi` へ自分を登録する。外すと HUD 無しで動く | 常に |
| `Fps.Editor` | JSON のインポーター、出力先ルート、ビルダーの共通の処理、キャラクターのリグ、入口のアセットの作成、差し替えの点検 | Editor |
| `Fps.Protocol` | クライアントと .NET の専用サーバーが送り合うメッセージと通信路 (UDP は LiteNetLib)。Unity に依存しない | 常に |
| `Fps.NetV2.Client` | ネット対戦のクライアントの中身 (つなぐ・予測・補間・クイックマッチのロビーの検索)。Unity に依存しない | 常に |
| `Fps.NetV2` | ネット対戦のクライアント (起動引数・メニューのクイックマッチ・自分と他のプレイヤー・HUD) | 常に |
| `Fps.NetEos` | EOS のログイン・ロビーの検索・P2P の通信・アンチチート | EOS |
| `Fps.Core.Tests`・`Fps.Tests.EditMode` | EditMode テスト | `testables` に入れたとき |
| `Fps.Samples.Editor` (サンプル) | シーンと仮の素材のビルダー、smoke、クライアントのビルド | サンプルを取り込んだとき (Editor) |
| `Fps.Samples.Net.Editor` (サンプル) | ネット対戦のマップと試合の設定のビルダー、専用サーバーへのルールと出現地点の書き出し | サンプルを取り込んだとき (Editor) |
| `Fps.Samples.Tests`・`Fps.Samples.Net.Tests` (サンプル) | サンプルの EditMode テスト | サンプルを取り込んだとき |
