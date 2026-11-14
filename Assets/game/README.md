# FPS Kit (`com.omnistove.fps`)

一人称・三人称のシューターのゲームプレイ (武器・敵 AI・モード・キャンペーン) と、シーンやアセットを作る Editor のスクリプトをまとめた UPM パッケージ。
ここに書いた手順だけで、空の Unity プロジェクトに入れて、EditMode テスト・武器 JSON の取り込み・smoke の実行まで進められる。

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
- 武器の音と光の素材を作る `Fps/Build Presentation Assets` だけは URP (`com.unity.render-pipelines.universal`) のシェーダーを使う。URP が無くてもほかの機能と smoke は動き、武器の音と光が出ないという警告が出るだけ。

### 任意: ネット対戦 (`Fps.Net`) と EOS (`Fps.NetEos`)

ネットまわりのアセンブリは、必要なパッケージがプロジェクトにあるときだけコンパイルされる (asmdef の `versionDefines` と `defineConstraints`)。入れなければ、そのアセンブリとテストは無いものとして扱われ、コンパイルエラーにもならない。

| アセンブリ | 有効になる条件 | manifest に足す行 |
| --- | --- | --- |
| `Fps.Net`・`Fps.Net.Editor`・`Fps.Net.Tests` | `com.unity.netcode.gameobjects` 2.14.0 以上 | `"com.unity.netcode.gameobjects": "2.14.0"` |
| `Fps.NetEos` | 上に加えて `com.playeveryware.eos` | `"com.playeveryware.eos": "https://github.com/EOS-Contrib/eos_plugin_for_unity_upm.git"` |

smoke の最後の 1 本 (`PerfSmokeRun`) は `Fps.Net.Editor` にあるので、**smoke をすべて通すには Netcode も入れる**こと ([動作確認](#動作確認-smoke) を参照)。

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
3. 既定の `Assets/game`

パッケージの中は書き込めないので、値は `Assets/` の下のフォルダに限る。`Assets` そのもの、`Packages/...`、`..` を含むパス、`-fpsKitRoot` の値が無い・2 度ある、のどれかなら `ArgumentException` で止まる。
同梱のサンプル JSON などは出力先ルートとは関係なく、パッケージの中 (`Packages/com.omnistove.fps/`) から読む。

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
  -assemblyNames "Fps.Core.Tests;Fps.Tests.EditMode" \
  -testResults "$PWD/fps-editmode-results.xml" -logFile -
```

- 終了コードが 0 ならすべて通った。0 以外なら失敗で、どのテストが落ちたかは `fps-editmode-results.xml` (NUnit 形式) にある。
- Netcode を入れたなら、`-assemblyNames` に `Fps.Net.Tests` を足すとネットのテストも回る。`-assemblyNames` を省くと、プロジェクトにあるすべての EditMode テストが回る。
- テストは `Assets/__<テスト名>_<GUID>` のような一時フォルダにアセットを作り、終わると消す。

Editor からは **Window > General > Test Runner** の EditMode タブで同じテストを回せる。

## データの差し込み

### 武器を JSON から取り込む

1 ファイルが武器 1 つで、項目名は `Fps.Core.WeaponSpec` のコンストラクタ引数と同じ。サンプルは `Packages/com.omnistove.fps/Weapons/Json/` にある (`Rifle.json`・`Handgun.json`・`Launcher.json`)。

```json
{
  "id": "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83",
  "displayName": "Rifle",
  "category": "AssaultRifle",
  "fireMode": "Auto",
  "magazineSize": 30,
  "...": "残りの項目はサンプルを参照"
}
```

- `category`・`fireMode` は列挙子の名前の文字列。
- `falloff` は `{ "startDistance": …, "endDistance": …, "minMultiplier": … }` か、減衰しないなら `"None"`。
- `adsFieldOfView` と `displayName` は `null` も書ける。既定値のある項目は省ける。知らない項目はエラーになる。

batchmode で取り込む:

```sh
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Fps.Editor.WeaponJsonImporter.Import \
  -weaponJson Packages/com.omnistove.fps/Weapons/Json \
  -weaponOut Assets/FpsKit/Weapons \
  -logFile -
```

| 引数 | 必須 | 意味 |
| --- | --- | --- |
| `-weaponJson <file\|dir>` | 必須 | JSON 1 つか、直下の `*.json` を名前の順に読むフォルダ。プロジェクトからの相対か絶対パス。`Packages/com.omnistove.fps/...` もそのまま渡せる |
| `-weaponOut <dir>` | 省略可 | アセットを置く `Assets/` の下のフォルダ。省くと `<出力先ルート>/Weapons` (`-fpsKitRoot` が効く) |
| `-weaponCatalog <asset>` | 省略可 | 取り込んだ武器を Id で重複なく足す `WeaponCatalogAsset` のパス (例: `Assets/game/Weapons/WeaponCatalog.asset`)。そのカタログが既に無いとエラー |

- アセットの名前は JSON のファイル名 (`Rifle.json` → `Rifle.asset`)。既にあれば GUID を保って中身だけ更新するので、参照しているシーンやプレハブは壊れない。
- 成功するとログに `[WeaponJsonImporter] saved '<アセット>' (id '<Id>') from '<JSON>'` が出て、終了コード 0 で終わる。
- 値が不正・項目が欠けている・知らない項目がある・Id が重複している、などのときは、ファイル名と項目名を添えた例外がログに出て、**1 つも保存せずに** 終了コード 1 で終わる。

Editor からは **Fps > Import Weapons From JSON** でフォルダを選ぶ (出力先は `<出力先ルート>/Weapons`、カタログがあれば足すかを聞かれる)。

### モードのルールを JSON から取り込む

1 ファイルがルール 1 つ (`GameRulesDefinition`)。サンプルは `Packages/com.omnistove.fps/Rules/Json/` にある (`TeamDeathmatch`・`Domination`・`Range`・`TpsRange`・`CampaignEasy`/`Normal`/`Hard`)。

```sh
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod Fps.Editor.GameRulesJsonImporter.Import \
  -rulesJson Packages/com.omnistove.fps/Rules/Json \
  -rulesOut Assets/FpsKit/Rules \
  -logFile -
```

| 引数 | 必須 | 意味 |
| --- | --- | --- |
| `-rulesJson <file\|dir>` | 必須 | JSON 1 つか、直下の `*.json` を名前の順に読むフォルダ |
| `-rulesOut <dir>` | 省略可 | アセットを置く `Assets/` の下のフォルダ。省くと `<出力先ルート>/Rules` |

成功すると `[GameRulesJsonImporter] saved '...'` が出る。失敗したときの扱い (1 つも保存せず終了コード 1) は武器と同じ。
Editor からは **Fps > Import Game Rules From JSON**。

### Inspector から変えられる値

取り込んだアセットや、ビルダーが置いたコンポーネントは、Inspector で値を変えられる。どの項目にも Tooltip が付いている。

| 対象 | 主な値 |
| --- | --- |
| `WeaponDefinition` (`<root>/Weapons/*.asset`) | 弾数・連射間隔・リロード・ダメージ・射程・距離減衰・爆発半径・ADS の視野角・ブラインドファイアの散らばり・アクティブリロードの判定・反動・弾の散らばり |
| `WeaponCatalogAsset`・`LoadoutDefinition` | 使える武器の一覧と、最初に持つ武器 |
| `GameRulesDefinition` (`<root>/Rules/*.asset`) | フレンドリーファイア・リスポーン・自動回復・死亡時の見せ方・弾薬・建造物の破壊・ダウンやカバーなどのアクション・スポーン保護・落下ダメージ・HUD の量・勝敗の決め方 |
| `GrenadeThrower` (プレイヤーのコンポーネント) | 爆発までの秒数・投げる間隔・初速・ダメージ・爆発の半径・建造物への被害 |

JSON から取り込み直すと、そのアセットの値は JSON の値で上書きされる。Inspector で変えた値を残したいなら、JSON にも同じ値を書く。

## 動作確認 (smoke)

`Fps.Editor.SmokeSuite.RunAll` は、次の SmokeRun を 1 つの Editor で順に Play Mode に入れて走らせる。必要な武器・ルール・シーンは各 SmokeRun が出力先ルートに作るので、空のプロジェクトでもそのまま回せる。

1. `CampaignSmokeRun`
2. `DestructionSmokeRun`
3. `EnemyFireSmokeRun`
4. `EnemyStuckSmokeRun`
5. `WeaponSwitchSmokeRun`
6. `PerfSmokeRun` (`Fps.Net.Editor`。Netcode が要る。サーバーの tick の平均・最大・p95 を `Logs/perf.json` に書き、予算を超えると失敗)

Play Mode に入るので、**`-quit` を付けない** (付けるとすぐに終了コード 1 で終わる)。

```sh
"$UNITY" -batchmode -projectPath . \
  -executeMethod Fps.Editor.SmokeSuite.RunAll \
  -fpsKitRoot Assets/FpsKit \
  -logFile -
```

`-fpsKitRoot` を省くと、生成物は `Assets/game` に置かれる。

### 成功と失敗の見分け方

**終了コード** で見分ける: すべて通れば 0、1 つでも失敗すれば 1。ログの最後に `[SmokeSuite]` のまとめが出る。

成功:

```
[SmokeSuite] passed:
  passed CampaignSmokeRun (exit code 0)
  passed DestructionSmokeRun (exit code 0)
  passed EnemyFireSmokeRun (exit code 0)
  passed EnemyStuckSmokeRun (exit code 0)
  passed WeaponSwitchSmokeRun (exit code 0)
  passed PerfSmokeRun (exit code 0) (exit code 0)
```

失敗:

```
[SmokeSuite] failed:
  passed CampaignSmokeRun (exit code 0)
  FAILED DestructionSmokeRun (exit code 1)
  ...
  NOT RUN PerfSmokeRun (exit code 1)
```

- `FAILED <名前>` の行の SmokeRun が落ちた。その名前のタグ (`[DestructionSmokeRun]` など) でログを探すと理由がある。1 つが 600 秒を超えても終わらないときも、`[SmokeSuite] <名前> did not finish within 600s` を出して `FAILED` になる。
- `NOT RUN <名前>` は結果を受け取れないままスイートが終わった SmokeRun で、これも失敗に数える。
- Netcode を入れていないと `PerfSmokeRun` が `smoke run type 'Fps.Editor.PerfSmokeRun, Fps.Net.Editor' was not found` で `FAILED` になり、スイート全体も 1 で終わる。残りの 5 本が `passed` なら、Netcode 以外は動いている。

## アセンブリの一覧

| アセンブリ | 中身 | 有効になる条件 |
| --- | --- | --- |
| `Fps.Core` | Unity に依存しないルールと計算 | 常に |
| `Fps.Runtime` | ゲームプレイのコンポーネントとアセットの型 | 常に |
| `Fps.Runtime.UI` | 既定の UI (HUD・メニュー・ポーズ・設定)。起動時に `GameUi` へ自分を登録する。外すと HUD 無しで動く | 常に |
| `Fps.Editor` | シーンとアセットのビルダー、JSON のインポーター、smoke | Editor |
| `Fps.Net`・`Fps.Net.Editor` | Netcode for GameObjects のネット対戦 | Netcode 2.14.0 以上 |
| `Fps.NetEos` | EOS のログイン・ロビー・P2P の通信・アンチチート | Netcode と EOS |
| `Fps.Core.Tests`・`Fps.Tests.EditMode` | EditMode テスト | `testables` に入れたとき |
| `Fps.Net.Tests` | ネットの EditMode テスト | `testables` に入れ、Netcode もあるとき |
