# FPS Kit (`com.omnistove.fps`)

一人称・三人称のシューターのゲームプレイと、シーンやアセットを作る Editor のスクリプトをまとめた UPM パッケージ。
ゲームごとの中身 (武器・ルール・ミッション・シーン・プレハブ) と、既定の UI・言語の表は、サンプル **Demo Game** (`Samples~/Demo`) にある。

この README には導入と差し替えの手順だけを書く。ほかは次の正本を見る。

| 知りたいこと | 正本 |
| --- | --- |
| Unity の版・依存パッケージ | `package.json` |
| コマンド (テスト・取り込み・smoke・点検) と引数 | `mise tasks` (`.mise/tasks.toml`) |
| 武器・ルールの JSON の項目 | サンプルの `Weapons/Json/`・`Rules/Json/` と、`Fps.Core` の Spec |
| 調整値の JSON の項目と既定値 | `Resources/Tuning/*.json` |
| 入口・アセット・コンポーネントの欄 | 各型の Inspector の Tooltip |
| 文言のキー | サンプルの `Resources/Localization/ja/` |
| 専用サーバー | `docs/dedicated-server.md` |

## 導入

1. `Packages/manifest.json` の `dependencies` に足す。`#` の後ろはタグか SHA にし、`Packages/packages-lock.json` もコミットする。

   ```json
   "com.omnistove.fps": "https://github.com/omni-stove/fps.git?path=/Assets/game#v0.1.0"
   ```

2. **Edit > Project Settings > Player > Active Input Handling** を `Input System Package (New)` か `Both` にする。
3. テストも回すなら、manifest に `"testables": ["com.omnistove.fps"]` を足す。

EOS (`Fps.NetEos`) は `com.playeveryware.eos` を入れたときだけコンパイルされる。

## サンプル

1. **Window > Package Manager** で FPS Kit の **Samples** から **Demo Game** を **Import** する。
2. **Fps > Build Main Menu Scene** を一度実行する。

このリポジトリでは `Assets/Samples/FpsKitDemo` が `Assets/game/Samples~/Demo` へのシンボリックリンクなので、サンプルは `Samples~/Demo` の下を直す。

## ゲームの中身を差し替える

1. 入口のアセット `GameContent` を作る (`make game-content` か **Fps > Create Game Content**)。
2. 入口の欄に自分のアセットを入れる。
3. `make audit-replacements` で差し替え漏れを点検する。

- 入口は `Resources/FpsKit/` に 1 つだけ置く。
- シーンのコンポーネントに配線した参照は入口より先に使われる。入口に頼るシーンではその参照を空にする。
- 調整値の JSON は、パッケージの `Resources/Tuning/` と同じパスに置かず、入口の欄に入れる。同じパスだとどちらが読まれるか決まらない。

### UI

キットのゲーム進行は画面も文も作らない。今の状態は型の付いた値で `GameSignals` (`Fps.Runtime`) に出し、1 回きりの出来事 (撃った・当たった・倒した) は `CombatEvents` のイベントで知らせる。何をどう出すか (言語を含む) は UI が決める。
既定の UI (`Fps.Runtime.UI`) と言語の表は Demo サンプルにあり、サンプルを取り込まなければ HUD 無しで動く。

#### `GameSignals` の項目

| 項目 | 型 | 中身 (無いときは null) |
| --- | --- | --- |
| `LocalPlayer` | `LocalPlayerSignal` | この画面で操作しているプレイヤーの部品 (`Damageable`・`Weapon`・`Ads`・`Mover`・`Grenades`・`Abilities`・`Actions`) |
| `HudModel` | `HudModel` | 体力・弾・試合の残り時間・得点・結果などの数。変わった項目を `HudModel.Changed` (`HudField`) で知らせる |
| `Inventory` | `IInventoryControl` | 持ち物。部品に持ち物の無い画面 (ネット対戦) だけが出す |
| `GrenadeCount` | `GrenadeCountStatus?` | 残りのグレネード。`GrenadeThrower` の無い画面 (ネット対戦) だけが出す |
| `LocalBlindSeconds` | `Func<float>` | 目くらましの残り秒数を読む関数。ネット対戦だけが出す |
| `Participants` | `IParticipants` | `CombatEvents.Killed` の Id を参加者に引く口。ネット対戦だけが出す |
| `ModeStatus` | `ModeStatus` | オフラインのモードの残り時間・陣営ごとの得点・自分の陣営・リスポーンまでの秒数・勝敗と、モード固有の状態 `Detail` |
| `Domination` | `DominationStatus` | 拠点制圧の試合の状態 |
| `Scoreboard` | `IReadOnlyList<ScoreboardEntry>` | スコアボードの行 |
| `InteractHint` | `InteractHint?` | 近くで Interact したら何が起きるか (`Source` と `Detail`) |
| `Tutorial` | `TutorialStatus?` | 射撃場のチュートリアルの段階 |
| `KillCam` | `KillCamStatus?` | 再生しているキルカム |
| `Pings` | `IReadOnlyList<PingMarker>` | この画面のプレイヤーが付けている印 |
| `SquadOrder` | `SquadOrderStatus?` | 味方の bot に出している指示 |
| `CampaignNotice` | `CampaignNotice` | キャンペーンが一時的に出す案内 |
| `MenuReturnReason` | `DisconnectNotice` | メニューに戻った理由 |
| `HudDensity` | `HudDensity?` | HUD に出す量 |
| `PauseTimeMode` | `PauseTimeMode?` | ポーズを開いている間の時間の扱い |
| `PaletteViewer` | `Team?` | 敵味方の色の組をどのチームから見て塗るか |

#### 購読のしかた

- 項目はどれも `GameSignal<T>` で、今の値 `Value`・値があるか `HasValue`・値を出したときと消したときに新しい値で呼ばれる `Changed` を持つ。同じ値を出し直しても `Changed` は呼ばれる。
- 先に出ていた値は `Changed` では来ないので、購読を始めたら `Value` を読んで合わせる。
- 値と購読者はシーンを読む前 (`SubsystemRegistration`) に空へ戻る。購読は `BeforeSceneLoad` の初期化か、コンポーネントの `OnEnable` で始め、`OnDisable` でやめる。
- 書くのはゲーム進行だけで、UI は `Publish` も `Clear` もしない。

体力だけを出す最小の HUD。`Assets/` の下のスクリプトに置いて Play すると、プレイヤーが出たときに左上に体力が出る。

```csharp
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

public sealed class HealthOnlyHud : MonoBehaviour
{
    private HudModel _model;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        DontDestroyOnLoad(new GameObject(nameof(HealthOnlyHud), typeof(HealthOnlyHud)));
    }

    private void OnEnable()
    {
        GameSignals.HudModel.Changed += Bind;
        Bind(GameSignals.HudModel.Value);
    }

    private void OnDisable()
    {
        GameSignals.HudModel.Changed -= Bind;
    }

    private void Bind(HudModel model)
    {
        _model = model;
    }

    private void OnGUI()
    {
        if (_model != null && _model.CurrentHealth.HasValue)
        {
            GUI.Label(new Rect(16, 16, 200, 24), _model.CurrentHealth.Value + " / " + _model.MaxHealth.Value);
        }
    }
}
```

サンプルを取り込んだまま自分の HUD を出すなら、同じ `Create` の中で `DefaultHudFactory.Enabled = false;` も入れ、既定の HUD を止める。

#### モード固有の表示

モードの Runner (`ModeRunnerBase` の派生) は、自分の Core の状態を `HudDetail` で返し、それが `ModeStatus.Detail` に入る。UI は `Detail` の型を見て何を出すかを決める。

| モード | `Detail` の型 |
| --- | --- |
| チームデスマッチ | `ModeSession` |
| Free for All | `FreeForAllStanding` |
| Kill Confirmed | `KillConfirmed` |
| Hardpoint | `Hardpoint` |
| 爆破 | `SearchAndDestroyStatus` |
| Execution | `ExecutionStatus` |
| Guardian | `GuardianStatus` |
| Gun Game | `GunGameStatus` |
| Battle Royale | `BattleRoyaleStatus` |
| Horde | `HordeStatus` |

サンプルの `ModeHudView` は、`Detail` の型ごとに `ModeHudFormatters` に登録した書式役 (`IModeHudFormatter`。土台は `ModeHudFormatterBase`) で文にする。

- 登録は `ModeHudFormatters.Register<TDetail>(formatter)` で、シーンを読む前 (`BeforeSceneLoad`。Edit Mode でも使うなら `InitializeOnLoadMethod` も) に済ませる。同じ型を登録し直すと置き換わる。
- 引くときは `Detail` の型、無ければその基底の型の順に探し、どれも無ければ残り時間・得点・リスポーンまでの秒数・結果だけを出す `ModeHudFormatters.Default` を使う。
- 自分のモードを足すなら、Runner の `HudDetail` で自分の状態の型を返し、その型の書式役を登録する。

#### UI からゲーム進行への口

ポーズ・ロードアウトの選択・持ち物・メニューとの行き来は、UI が `GameUi.Pause`・`LoadoutSelect`・`InventoryScreen`・`Menu` に自分の実装を入れる (サンプルは既定を `UseDefault…` で入れる)。
既定の HUD を止めたら `GameUi.Pause` は必須 (無いとポーズ中も操作が止まらない)。

## 乗り物を足す

キットは乗り降り・座席・耐久・座席の武器・固定銃座を持ち、動き方は `IVehicleMotor` で差し込む。同梱の動き方は車 (`SimpleCarMotor`) だけ。
組み方の例はサンプルの `CarFixtureBuilder`・`MountedGunFixtureBuilder` で、試走場は **Fps > Build Vehicle Range Scene**、通しの確認は `VehicleSmokeRun` (smoke のスイートに入っている)。

### 座席を並べる

1. 乗り物の根元に `Vehicle` を付け、その子に座席ごとの `VehicleSeat` を置く。座席の番号は子の並び順 (0 から)。
2. 座席の種類・姿勢・カメラは各 `VehicleSeat` の欄で決める。乗り物の値 (Id・耐久・部位・爆発・乗り込める距離・追うカメラ) は `VehicleDefinition` (Create > Fps > Vehicle Definition) に入れて `Vehicle` に配線する。コードで組むなら、座席と同じ番号の順に `VehicleSeatSpec` を並べた `VehicleSpec` を `Vehicle.Configure` に渡す。
   - 運転席 (`Driver`) の入力だけが動き方に届く。運転席が無ければ動かない乗り物 (固定銃座など) になる。
   - 座席の武器は、乗り物の子に `VehicleWeaponMount` を置き、操作する座席の番号をその欄に入れる。
   - 助手席から自分の武器で撃たせるなら、その座席の撃てる向きの範囲 (`OwnWeaponArcDegrees`) を 0 より大きくする。
3. 座席ごとに乗る位置 (`SetEnterPoints`) と降りる位置 (`SetExitPoints`、並びの順に試す) を置く。どの座席からでも使う降りる位置は `Vehicle.SetExitPoints`。

### 動き方を差し込む (`IVehicleMotor`)

1. `IVehicleMotor` を実装した MonoBehaviour を `Vehicle` と同じ GameObject に付け、`VehicleMotorLink` も付ける。`VehicleMotorLink` が運転席の入力を `VehicleMotorInput` (`Throttle`・`Steer`・`Brake`・`Vertical`) にして `SetInput` に渡す。
2. `SetInput` では入力を覚えるだけにし、力は `FixedUpdate` で掛ける。座席の入力は `Update` で届くので、その場で掛けると描画の頻度で物理が変わる。
3. 運転席が空くと `VehicleMotorInput.Idle` が届くので、そのときはアクセルなどを放す。
4. 調整値は自分の Spec や ScriptableObject に持たせ、`Configure` などで入れる (`SimpleCarMotor` と `CarSpec` が例)。

入力の対応: `Throttle` は移動の前後、`Steer` は左右、`Brake` はダッシュ、`Vertical` はジャンプ (+1) としゃがみ (-1)。空を飛ぶものは `Vertical` を上昇・下降に使う。
プレイヤーの座席の入力を AI・チュートリアル・自動の確認から書き換えるときは、`PlayerVehicleRider.SeatInputFilter` に `IVehicleSeatInputFilter` を入れる。

## 文言・言語 (サンプルの決まり)

キットは言語と文を扱わない。武器などの表示名はキー (ただの文字列) としてデータに持ち、それを文にするのは UI。ここからはサンプルの UI (`Localizer`・`GameText`・`LocalizedTexts`) の決まりで、自分の UI を作るなら従わなくてよい。

文言は、サンプルの `Resources/Strings/<言語コード>.json` の後に `Resources/Localization/<言語コード>/*.json` をファイル名の順に重ね、同じキーは後のファイルが勝つ。`Resources/FpsKit/` の `LocalizationContent` (**Create > Fps > Localization Content**) に言語ごとの表を入れると、さらにその後に重なる。
利用側のファイルは、サンプルのどのファイルより後に並ぶ名前 (`zz-mygame.json` など) にする。同じ名前だとどちらが読まれるか決まらない。

- 文を変える: `Assets/Resources/Localization/ja/zz-mygame.json` に、変えるキーだけを書く。
- 言語を足す: `Assets/Resources/Localization/Languages/zz-mygame.json` に言語を書き、`Assets/Resources/Localization/<言語コード>/` に `ja` と同じキーの表を置く。表が 1 つでもある言語が設定画面で選べるようになる。

  ```json
  { "languages": [ { "code": "fr", "name": "Français", "systemLanguages": ["French"] } ] }
  ```

- 選んだ言語: 設定画面で選ぶと `LanguagePreferenceStore` が `PlayerPrefs` の `Fps.Language` に言語コードで保存する。キットの操作設定 (`ControlSettings`) には入れない。保存が無ければ OS の言語から決め (言語の一覧の `systemLanguages` に合う言語、どれにも合わなければ `otherSystemLanguages`)、前の形式 (操作設定の中の `language`) が残っていれば最初に読んだときに `Fps.Language` へ移す。
