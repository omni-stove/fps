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
- 武器の音と光の素材を作る `Fps/Build Presentation Assets` だけは URP (`com.unity.render-pipelines.universal`) のシェーダーを使う。URP が無くてもほかの機能と smoke は動き、武器の音と光が出ないという警告が出るだけ。薬莢と空撃ちの音の素材 (`Presentation/WeaponExtrasCatalog`) も同じく URP のシェーダーを使い、`Fps/Build All Presentation Assets` (`Fps.Editor.PresentationAssetSteps.BuildAll`) で作る。無ければ薬莢と空撃ちの音が出ないという警告が出るだけ。材質ごとの銃痕と着弾の素材 (`Presentation/SurfaceImpactCatalog`) も同じシェーダーを使い、同じ `Fps/Build All Presentation Assets` で作る。無ければ着弾は従来の火花と音になる。

### 任意: ネット対戦 (`Fps.Net`) と EOS (`Fps.NetEos`)

ネットまわりのアセンブリは、必要なパッケージがプロジェクトにあるときだけコンパイルされる (asmdef の `versionDefines` と `defineConstraints`)。入れなければ、そのアセンブリとテストは無いものとして扱われ、コンパイルエラーにもならない。

| アセンブリ | 有効になる条件 | manifest に足す行 |
| --- | --- | --- |
| `Fps.Net`・`Fps.Net.UI`・`Fps.Net.Editor`・`Fps.Net.Tests` | `com.unity.netcode.gameobjects` 2.14.0 以上 | `"com.unity.netcode.gameobjects": "2.14.0"` |
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
  "nameKey": "weapon.rifle",
  "category": "AssaultRifle",
  "fireMode": "Auto",
  "magazineSize": 30,
  "...": "残りの項目はサンプルを参照"
}
```

- `category`・`fireMode` は列挙子の名前の文字列。
- `falloff` は `{ "startDistance": …, "endDistance": …, "minMultiplier": … }` か、減衰しないなら `"None"`。
- `nameKey` は画面に出す武器の名前を引く表のキー。文は `Resources/Localization/<言語>/` の JSON (例: `items.json` に `"weapon.rifle": "Rifle"`) に書く。表に無いキーはキーそのものが出る。空か `null` なら `id` が出る。
- 前の項目名 `displayName` は読まない (書き換えを促すエラーになる)。
- `adsFieldOfView` と `nameKey` は `null` も書ける。既定値のある項目は省ける。知らない項目はエラーになる。
- `recoilRecoveryDelaySeconds` (撃ってから反動が戻り始めるまでの秒数) はサンプルでは省いていて、既定は 0.15。

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
| `PingController` (プレイヤーのコンポーネント) | 印が消えるまでの秒数・同時に付けられる数・照準の先を探す距離・照準が外れていても敵に付ける角度 |
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

## UI を差し替える

ゲーム進行 (`Fps.Runtime`・`Fps.Net`) は、画面を具体的なクラスでは持たず、登録口 `Fps.Runtime.GameUi`・`Fps.Net.NetUi` に入っている実装を使う。
既定の UI (`Fps.Runtime.UI`・`Fps.Net.UI`) は起動時に自分を **既定** として登録するだけなので、使う側が登録口のプロパティに自分の実装を入れれば、既定の UI のコードは呼ばれなくなる。

| 登録口のプロパティ | 実装するインターフェース | 使われる所 | 既定 |
| --- | --- | --- | --- |
| `GameUi.HudFactory` | `IHudFactory` (作るのは `IHud`) | オフラインの HUD とプレイヤーのデバッグ表示 (`GameBootstrap` が作る) | `Fps.Runtime.UI` の HUD。ポーズメニュー・ロードアウトの選択・字幕なども、この HUD の GameObject に載っている |
| `GameUi.Pause` | `IPauseState` | ポーズ画面が開いているか (開いている間は操作を止める) | 既定の HUD のポーズメニュー |
| `GameUi.LoadoutSelect` | `ILoadoutSelect` | カーソルを解放してロードアウトの画面を開いているか | 既定の HUD のロードアウトの画面 |
| `GameUi.Menu` | `IMenuNavigator` | メインメニューへ戻る・メニューで選んだロビーやチュートリアルを受け取る | `Fps.Runtime.UI` のメニューのシーンとの行き来 |
| `NetUi.HudFactory` (Netcode を入れたときだけ) | `INetHudFactory` (作るのは `INetHud`) | オンラインの自分のプレイヤーの HUD (`NetPlayer` が作る) | `Fps.Net.UI` の HUD |
| `PingUi.ViewFactory` | `IPingViewFactory` (作るのは `IPingView`) | オフラインのプレイヤーが付けた印 (`PingController`) を画面に出す。画面の外の印は端に寄せて向きを出す | 既定の HUD (`Hud`) に付く `PingMarkersView`。ほかの HUD には出さない (`null` を返す) |
| `SquadOrderUi.ViewFactory` | `ISquadOrderViewFactory` (作るのは `ISquadOrderView`) | プレイヤーが味方の bot に出した指示 (`SquadCommandController`) の守る場所・狙う敵を画面に出す | 既定の HUD (`Hud`) に付く `SquadOrderMarkerView`。ほかの HUD には出さない (`null` を返す) |

- 入れた実装は、登録の順番に依らず既定より優先される。`null` を入れると既定に戻る。
- 既定も自分の実装も無いときは、例外にせず HUD 無しで進む (ログに `no HUD factory is registered` などが出る)。生成役が `null` を返したときだけ `InvalidOperationException` になる。
- オフラインの `GameUi.HudFactory` とオンラインの `NetUi.HudFactory` は別のもの。両方を差し替えるなら両方に入れる。
- 既定の HUD を差し替えると、その HUD に載っていたポーズメニューやロードアウトの画面も出なくなる。自分でポーズ画面を作るなら `GameUi.Pause` にも、その開閉を返す実装を入れる (入れないと既定の `IsAnyOpen` は常に false で、ポーズ中も操作が止まらない)。
- `IHud` の `Scoreboard`・`HitMarker`・`ModeHud`・`TutorialPrompt`・`AttachDomination()` は、持っていなければ `null` を返してよい。ゲーム進行はその表示を出さずに進む。`AttachGrenadeCount()` と `AttachAbilitySlots()` は `null` を返さない (プレイヤーがグレネードや能力の枠を持つと、返した値の `Show` をすぐ呼ぶ)。
- 登録はシーンを読む前に済ませる。`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` を付けた static メソッドで入れるのが簡単。

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

### オンラインの HUD (Netcode を入れたとき)

`Fps.Net` は Netcode があるときだけコンパイルされるので、このコードも Netcode を入れたプロジェクトに置く (自分の asmdef に置くなら `references` に `Fps.Core`・`Fps.Runtime`・`Fps.Net` を足す)。
`NetPlayer` は自分のプレイヤーが出たときに `Create` を呼び、プレイヤーが消えるときに返した HUD の GameObject を消す。

```csharp
using Fps.Net;
using UnityEngine;

public sealed class MyNetHud : MonoBehaviour, INetHud
{
    private float _disconnectAt = -1f;

    public void ShowIdleWarning(float remainingSeconds)
    {
        _disconnectAt = Time.unscaledTime + (float.IsNaN(remainingSeconds) ? 0f : Mathf.Max(0f, remainingSeconds));
    }

    public void ClearIdleWarning() => _disconnectAt = -1f;

    private void OnGUI()
    {
        GUILayout.Label("MY NET HUD");
        if (_disconnectAt >= 0f)
        {
            var seconds = Mathf.CeilToInt(Mathf.Max(0f, _disconnectAt - Time.unscaledTime));
            GUILayout.Label(Fps.Runtime.Localizer.Format("hud.idleWarning", seconds));
        }
    }
}

public sealed class MyNetHudFactory : INetHudFactory
{
    public INetHud Create(NetPlayer player)
    {
        return new GameObject("MyNetHud").AddComponent<MyNetHud>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        NetUi.HudFactory = new MyNetHudFactory();
    }
}
```

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
- 言語は、初めて起動したときに OS の言語から選ばれる (日本語なら `ja`、それ以外は `en`)。`ja` の上書きを確かめるときに `en` が出ていたら、ポーズ > 設定の言語で日本語を選ぶ (選んだ言語は保存される)。
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

設定画面の言語の選択に出るのは、`Resources/Localization/<言語コード>/` に表が 1 つでもある言語。選べる言語コードはパッケージの `Fps.Core.GameLanguage` で決まっていて、今は `ja` と `en` だけ。

- `en` の文をそろえる・直すときは、`Assets/Resources/Localization/en/` に上と同じ形で JSON を置く。`en` に無いキーは `ja` の文にはならず、キーそのものが出る。
- `ja`・`en` 以外の言語 (例: `fr`) は、利用側に `Resources/Localization/fr/` を置いても読まれない。足すにはパッケージの `GameLanguage`・`GameLanguages` に言語を加える変更が要る。

### 訳さないもの

次の文は表を通さず、言語の設定に依らずコードに書いたままの文で出る。表にキーは無く、JSON で上書きもできない。

- ログ (`Debug.Log`・警告・エラー。`[GameUi]`・`[Localization]` などのタグ付きのものを含む) と、サーバーのログ・`Logs/` に書くファイル
- 例外のメッセージ (`ArgumentException`・`InvalidOperationException` など)
- エディタ向けの文 (`Fps` のメニュー・Inspector の Tooltip・ビルダーやインポーターのダイアログとログ)
- デバッグ表示 (プレイヤーのアクションのデバッグ表示・ルールのデバッグ用の欄・F3 の回線表示)

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
| `Presentation` | `Resources/Presentation/PresentationCatalog` が無い・`Validate` に通らない。`Assets/` の下の `WeaponDefinition` の Id にカタログの音とエフェクトが無い。武器の Id が空 |
| `Strings` | `ja`・`en` のどちらかに表が無い・壊れている。どちらかの言語にあってもう一方に無いキー。武器の `nameKey` が空か、どちらかの言語の表に無い |
| `Reference` | ビルドの一覧のシーン (有効なもの)・`Assets/` の下のプレハブ・アセットで、`[RequiredReference]` を付けた参照が空 (消えたアセットを指すものも含む)。ビルドの一覧のシーンが無い |

`[RequiredReference]` (`Fps.Runtime.RequiredReferenceAttribute`) は、空だと動かない参照 (空なら仮の形や既定値で続けるものは除く) に付ける印。今は `GunGameRunner` の Ladder・`HordeRunner` の Spawner・`WaveSpawner` の Enemy Template・`MissionRunner` の Definition・`EnemyAgent` の Weapon Definition に付いている。ゲームのコンポーネントやアセットのフィールドに付けても同じく見る。

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
| `Fps.Editor` | シーンとアセットのビルダー、JSON のインポーター、smoke | Editor |
| `Fps.Net`・`Fps.Net.Editor` | Netcode for GameObjects のネット対戦 | Netcode 2.14.0 以上 |
| `Fps.Net.UI` | ネット対戦の HUD (キルフィードの名前・スコアボード・F3 の回線表示・装備選択・ネームプレート・拠点)。起動時に `NetUi` へ自分を登録する。外すと HUD 無しで動く | Netcode 2.14.0 以上 |
| `Fps.NetEos` | EOS のログイン・ロビー・P2P の通信・アンチチート | Netcode と EOS |
| `Fps.Core.Tests`・`Fps.Tests.EditMode` | EditMode テスト | `testables` に入れたとき |
| `Fps.Net.Tests` | ネットの EditMode テスト | `testables` に入れ、Netcode もあるとき |
