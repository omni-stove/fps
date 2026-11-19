# Shooter Kit (`com.omnistove.shooter-kit`)

一人称・三人称のシューターのゲームプレイと、シーンやアセットを作る Editor のスクリプトをまとめた UPM パッケージ。
ゲームごとの中身 (武器・ルール・ミッション・シーン・プレハブ) と、既定の UI・言語の表は、サンプル **Demo Game** (`Samples~/Demo`) にある。

この README には導入と差し替えの手順だけを書く。ほかは次の正本を見る。

| 知りたいこと | 正本 |
| --- | --- |
| Unity の版・依存パッケージ | `package.json` |
| コマンド (テスト・取り込み・smoke・点検) と引数 | `mise tasks` (`.mise/tasks.toml`) |
| 武器・ルールの JSON の項目 | サンプルの `Weapons/Json/`・`Rules/Json/` と、`Shooter.Core` の Spec |
| 調整値の JSON の項目と既定値 | `Resources/Tuning/*.json` |
| 入口・アセット・コンポーネントの欄 | 各型の Inspector の Tooltip |
| 文言のキー | サンプルの `Resources/Localization/ja/` |
| 専用サーバー | `docs/dedicated-server.md` |

## 導入

1. `Packages/manifest.json` の `dependencies` に足す。`#` の後ろはタグか SHA にし、`Packages/packages-lock.json` もコミットする。

   ```json
   "com.omnistove.shooter-kit": "https://github.com/omni-stove/shooter-kit.git?path=/Assets/game#v0.1.0"
   ```

2. **Edit > Project Settings > Player > Active Input Handling** を `Input System Package (New)` か `Both` にする。
3. テストも回すなら、manifest に `"testables": ["com.omnistove.shooter-kit"]` を足す。

EOS (`Shooter.NetEos`) は `com.playeveryware.eos` を入れたときだけコンパイルされる。

### クラウドセーブ

プロフィール (`profile`) とキャンペーンのセーブ (`campaign`・`campaign-<ミッション Id>`) を EOS の Player Data Storage と同期する。

1. EOS の開発者ポータルで Player Data Storage を使えるようにする。
2. ほかの認証情報と同じく、環境変数か `eos.local.json` に `EOS_ENCRYPTION_KEY` (16 進 64 文字) を入れる。どの端末でも同じ鍵にする。鍵が違う端末からは読めない。

- 保存はいつも手元に書き、同期はその後で行う。EOS が無いビルド・認証情報か鍵が無いとき・ログインできないとき (オフライン) は手元だけで動き、次にログインしたときに合わせる。
- 起動時に Device ID で EOS にログインし、ログインできたらクラウドの一覧と手元を突き合わせる。保存するたびにそのスロットを送る。
- 手元とクラウドが食い違ったら、書いた時刻 (UTC) の新しい方を採る。消したことも時刻付きで送るので、他の端末で消したセーブは戻ってこない。
- クラウドの方で手元を書き換えたときは、前の手元の中身を `cloud-replaced-<スロット>` に残す。
- Device ID は端末ごとの Id なので、別の端末と同じアカウントで同期するには、Epic アカウントなどで同じ EOS のユーザーにログインさせる必要がある。
- 自分の保存先で同期するなら、`ICloudSaveBackend` を実装して `CloudSaveBootstrap.Backend` に `SubsystemRegistration` の初期化で入れる。

## サンプル

1. **Window > Package Manager** で Shooter Kit の **Samples** から **Demo Game** を **Import** する。
2. **Shooter > Build Main Menu Scene** を一度実行する。

このリポジトリでは `Assets/Samples/ShooterKitDemo` が `Assets/game/Samples~/Demo` へのシンボリックリンクなので、サンプルは `Samples~/Demo` の下を直す。

## ゲームの中身を差し替える

1. 入口のアセット `GameContent` を作る (`make game-content` か **Shooter > Create Game Content**)。
2. 入口の欄に自分のアセットを入れる。
3. `make audit-replacements` で差し替え漏れを点検する。

- 入口は `Resources/ShooterKit/` に 1 つだけ置く。
- シーンのコンポーネントに配線した参照は入口より先に使われる。入口に頼るシーンではその参照を空にする。
- 調整値の JSON は、パッケージの `Resources/Tuning/` と同じパスに置かず、入口の欄に入れる。同じパスだとどちらが読まれるか決まらない。
  - 入口を作るときは、`Assets/` の下で名前がキットの調整値と同じ (`minimap.json` など) ファイルが 1 つだけなら、その欄に入る。

### スキン・迷彩

1. 見た目の一覧の JSON (`Tuning/cosmetics.json` と同じ形) を入口の `CosmeticsTuning` に入れる。キットの既定は空で、何も差し替えない。
2. **Create > Shooter > Cosmetic Visual Catalog** を作り、JSON の `visualId` ごとに、スキンなら体のプレハブ、迷彩なら武器に塗る材質を入れて、入口の `CosmeticVisuals` に入れる。
   - スキンの体は `NetV2/RemotePlayerBody` と同じく、根の原点を体の中心に合わせ、立った高さで作る。当たりは付けても捨てる。
   - 迷彩は一人称の武器のモデルの材質をすべて替える (粒子と線は替えない)。
3. 着けた見た目はプロフィールに残る。ネット対戦ではスキンだけをロードアウトと一緒に送り、相手の画面の体が替わる。カタログか絵が無い Id は既定の見た目のまま出る。

### UI

キットのゲーム進行は画面も文も作らない。今の状態は型の付いた値で `GameSignals` (`Shooter.Runtime`) に出し、1 回きりの出来事 (撃った・当たった・倒した) は `CombatEvents` のイベントで知らせる。何をどう出すか (言語を含む) は UI が決める。
既定の UI (`Shooter.Runtime.UI`) と言語の表は Demo サンプルにあり、サンプルを取り込まなければ HUD 無しで動く。

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
| `Streak` | `StreakStatus?` | この画面のプレイヤーのストリーク (倒れずに倒した数・報酬の枠・使える枠の数) |
| `Minimap` | `MinimapStatus?` | ミニマップとコンパスに出すもの (自分の位置と向き・味方/敵/印/目標の `MinimapContact` の一覧) |
| `Chat` | `IReadOnlyList<ChatLine>` | チャットの行 (送った人の参加者 Id・全体かチームか・本文・時刻) |
| `Voice` | `VoiceStatus?` | ボイスチャットの状態 (自分が話しているか・話している参加者 Id・マイクが使えるか) |
| `MapVote` | `MapVoteStatus?` | 次のマップの投票 (候補のマップ Id と票数・自分の票・残り秒数・決まったマップ Id) |
| `Spectate` | `SpectateStatus?` | ネット対戦で味方を見ている状態 (見ている参加者 Id・候補の数・理由) |
| `ChatSender` | `IChatSender` | チャットを全体かチームへ送る口 (`Send`)。ネット対戦だけが出す |
| `MapVoteSender` | `IMapVoteSender` | 次のマップに `MapVote` の候補を並びの番号で選ぶ口 (`Vote`)。ネット対戦だけが出す |
| `Party` | `PartyStatus?` | パーティの状態 (`PartyStage`・メンバーの名前とリーダーか・招待のコード・自分がリーダーか) |
| `PartyControl` | `IPartyControl` | パーティを作る・招待のコードで入る・出る口と、状態が変わった知らせ (`StatusChanged`) |
| `JoinByCode` | `IJoinByCode` | 参加コードで非公開の試合に入る口。結果 (`JoinCodeOutcome`) を呼び戻しで返す |
| `ReportSender` | `IReportSender` | 参加者 Id と理由 (`ReportReason`) で通報する口。ネット対戦だけが出す |
| `BlockControl` | `IBlockControl` | 参加者 Id でブロック (チャットを落とす)・個別ミュート (声を受けない) を切り替え・読む口と、変わった知らせ (`Changed`)。ネット対戦だけが出す。ボイスの名簿に載った人は EOS の ProductUserId で `BlockListStore` (PlayerPrefs) に残り、次の試合でも効く。載っていない人はその試合の間だけ効く |
| `Achievements` | `IReadOnlyList<AchievementProgress>` | 実績ごとの進み (実績の Id・今の数・目標の数・解けたか) |
| `Leaderboard` | `LeaderboardStatus?` | 開いているリーダーボード (ボードの Id・`LeaderboardStage`・順位と名前と得点の `LeaderboardRow` の一覧) |
| `LeaderboardControl` | `ILeaderboardControl` | `LeaderboardSpec` のボードの Id でリーダーボードを開く・読み直す・閉じる口。結果は `Leaderboard` に出る |
| `Replays` | `IReplayLibrary` | 残したリプレイの一覧 (`ReplayEntry`) と、再生・消す口 |
| `Cosmetics` | `ICosmeticLocker` | スキン・迷彩のカタログと、着けている Id・解けているかを読み、着ける口 |
| `Stash` | `StashStatus?` | 脱出の倉庫の中身 (広さと、持ち物の Id と数の `StashEntry` の一覧) |

#### 購読のしかた

- 項目はどれも `GameSignal<T>` で、今の値 `Value`・値があるか `HasValue`・値を出したときと消したときに新しい値で呼ばれる `Changed` を持つ。同じ値を出し直しても `Changed` は呼ばれる。
- 先に出ていた値は `Changed` では来ないので、購読を始めたら `Value` を読んで合わせる。
- 値と購読者はシーンを読む前 (`SubsystemRegistration`) に空へ戻る。購読は `BeforeSceneLoad` の初期化か、コンポーネントの `OnEnable` で始め、`OnDisable` でやめる。
- 書くのはゲーム進行だけで、UI は `Publish` も `Clear` もしない。

体力だけを出す最小の HUD。`Assets/` の下のスクリプトに置いて Play すると、プレイヤーが出たときに左上に体力が出る。

```csharp
using Shooter.Core;
using Shooter.Runtime;
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
| 旗取り | `CaptureTheFlagStatus` |
| Execution | `ExecutionStatus` |
| Guardian | `GuardianStatus` |
| Gun Game | `GunGameStatus` |
| Battle Royale | `BattleRoyaleStatus` |
| Horde | `HordeStatus` |
| Infected | `InfectedStatus` |
| 脱出 | `ExtractionStatus` |

サンプルの `ModeHudView` は、`Detail` の型ごとに `ModeHudFormatters` に登録した書式役 (`IModeHudFormatter`。土台は `ModeHudFormatterBase`) で文にする。

- 登録は `ModeHudFormatters.Register<TDetail>(formatter)` で、シーンを読む前 (`BeforeSceneLoad`。Edit Mode でも使うなら `InitializeOnLoadMethod` も) に済ませる。同じ型を登録し直すと置き換わる。
- 引くときは `Detail` の型、無ければその基底の型の順に探し、どれも無ければ残り時間・得点・リスポーンまでの秒数・結果だけを出す `ModeHudFormatters.Default` を使う。
- 自分のモードを足すなら、Runner の `HudDetail` で自分の状態の型を返し、その型の書式役を登録する。

#### UI からゲーム進行への口

ポーズ・ロードアウトの選択・持ち物・メニューとの行き来は、UI が `GameUi.Pause`・`LoadoutSelect`・`InventoryScreen`・`Menu` に自分の実装を入れる (サンプルは既定を `UseDefault…` で入れる)。
既定の HUD を止めたら `GameUi.Pause` は必須 (無いとポーズ中も操作が止まらない)。

#### メインメニューの画面 (サンプルの決まり)

統計・リーダーボード・リプレイ・見た目・持ち物庫のボタンは、`MainMenuPanels` に画面が登録された種類 (`MainMenuPanelKind`) だけが出る。
サンプルは `DefaultMenuScreens.RegisterMainMenuPanels` で 5 つとも登録する。

| 画面 | 開くときに読むもの | 無いとき |
| --- | --- | --- |
| 統計 (`StatsPanel`) | `ProgressionBootstrap` のプロフィールと実績 | 開けず、メニューに理由を出す |
| リーダーボード (`LeaderboardPanel`) | `GameSignals.LeaderboardControl` (EOS が使えるとき `NetV2OnlineServices` が出す) | 開いて「使えない」を出す |
| リプレイ (`ReplayPanel`) | `GameSignals.Replays` (NetV2 の起動で `NetV2Replays` が出す) | 開いて「使えない」を出す |
| 見た目 (`CosmeticsPanel`) | `GameSignals.Cosmetics` | 開けず、メニューに理由を出す |
| 持ち物庫 (`StashPanel`) | `GameSignals.Stash`、無ければ `StashStore` に残した中身 | 空の持ち物庫を出す |

- 自分の画面に替えるなら、同じ種類を `MainMenuPanels.Register(kind, slot => ...)` で登録し直す (後の登録が勝つ)。登録はメニューのシーンを読む前 (`BeforeSceneLoad`) に済ませる。
- ボタンを消すなら `MainMenuPanels.Unregister(kind)` を呼ぶ。

#### リプレイ

ネット対戦は、届いた知らせを `Application.persistentDataPath` の `Replays/` に 1 試合 1 ファイル (`*.replay`) で残す。古いものから消して `ReplayLibrary.DefaultMaxReplays` 本までにする。

- 書き始めるのはサーバーに承認されてから。承認の前に抜けた試合のファイルは消す。書けないときは残さずに試合を続ける。
- 一覧の再生は `NetV2Client.BeginReplay` が同じ部品で試合を流し直す。`Next`・`Previous` で人と自由なカメラを巡り、終わるとメニューに戻る。
- 版の違う (通信の版が違う) ファイルや壊れたファイルは、画面を替えずに一覧に再生できないと出す。
- 通しの確認は `ReplaySmokeRun` で、smoke のスイートには入っていない (`unity -batchmode -projectPath . -executeMethod Shooter.Editor.ReplaySmokeRun.Run -logFile -`。Play Mode に入るので `-quit` は付けない)。承認と切断だけの記録を `Replays/` に置き、一覧から再生して `NetArena01` を読み、メニューに戻るまでを見る。置いた記録は終わったときに消す。

#### 試合中の部品 (NetV2)

`NetV2ClientParts.Create` が作る部品に、天候と時刻 (`NetV2WeatherPart`。シーンの `WeatherController` に当てる) と通報 (`NetV2ReportPart`。`GameSignals.ReportSender` を出す) が入る。
リプレイの再生では通報の代わりに見方の部品 (`NetV2ReplayViewPart`) が入る。
サーバーの側で通報を残す・統計を送る・テレメトリを残すには、起動の引数で頼む (`docs/dedicated-server.md` の「通報・統計・テレメトリ」)。

## モードのシーン (サンプル)

モードのシーンとルールは、名前の一覧 `GameRulesAssetBuilder.ModeNames` (Infected・脱出 (`Extraction`) を含む 12 個) で揃える。

1. **Shooter > Build Game Rules Assets** で、モードごとのルールを `Rules/<名前>.asset` に作る。
2. **Shooter > Build <モード> Scene** (Infected なら **Build Infected Scene**、脱出なら **Build Extraction Scene**) でシーンを `Scenes/<名前>.unity` に作る。ルールが無ければそのモードの分だけ作る。
3. **Shooter > Register Build Scenes** で、作ったシーンを Build Settings に並べる (`BuildSceneList.Scenes` の順)。
4. シーンを開いて Play すると、そのモードが始まる。

- Infected はプレイヤーと bot 4 体が生存者で始まり、倒された人は感染者として戻る。ルールは `GameRulesAssetBuilder.InfectedRules`。
- 脱出は敵の bot 3 体の中で箱から物を拾い、開いた脱出地点に立ち続けると持ち物が持ち物庫へ入る。倒れたら戻らない。ルールは `GameRulesAssetBuilder.ExtractionRules`、箱から出る物は `Modes/ExtractionLoot.json`。
- 通しの確認は `InfectedSmokeRun` (感染者が選ばれ、持ち替えて、どちらかの陣営が勝つまで) と `ExtractionSmokeRun` (箱を開け、開いた地点に立って脱出するまで) で、どちらも smoke のスイートに入っている。脱出の確認は、走らせる前の持ち物庫 (`StashStore.DefaultKey`) を控えて空から始め、終わったら戻す。

## 乗り物を足す

キットは乗り降り・座席・耐久・座席の武器・固定銃座を持ち、動き方は `IVehicleMotor` で差し込む。同梱の動き方は車 (`SimpleCarMotor`) と飛行機 (`SimpleAircraftMotor`)。
組み方の例はサンプルの `CarFixtureBuilder`・`MountedGunFixtureBuilder`・`AircraftFixtureBuilder` で、試走場は **Shooter > Build Vehicle Range Scene**、通しの確認は `VehicleSmokeRun` (smoke のスイートに入っている)。

### 座席を並べる

1. 乗り物の根元に `Vehicle` を付け、その子に座席ごとの `VehicleSeat` を置く。座席の番号は子の並び順 (0 から)。
2. 座席の種類・姿勢・カメラは各 `VehicleSeat` の欄で決める。乗り物の値 (Id・耐久・部位・爆発・乗り込める距離・追うカメラ) は `VehicleDefinition` (Create > Shooter > Vehicle Definition) に入れて `Vehicle` に配線する。コードで組むなら、座席と同じ番号の順に `VehicleSeatSpec` を並べた `VehicleSpec` を `Vehicle.Configure` に渡す。
   - 運転席 (`Driver`) の入力だけが動き方に届く。運転席が無ければ動かない乗り物 (固定銃座など) になる。
   - 座席の武器は、乗り物の子に `VehicleWeaponMount` を置き、操作する座席の番号をその欄に入れる。
   - 助手席から自分の武器で撃たせるなら、その座席の撃てる向きの範囲 (`OwnWeaponArcDegrees`) を 0 より大きくする。
3. 座席ごとに乗る位置 (`SetEnterPoints`) と降りる位置 (`SetExitPoints`、並びの順に試す) を置く。どの座席からでも使う降りる位置は `Vehicle.SetExitPoints`。

### 動き方を差し込む (`IVehicleMotor`)

1. `IVehicleMotor` を実装した MonoBehaviour を `Vehicle` と同じ GameObject に付け、`VehicleMotorLink` も付ける。運転席の入力は、当て方 (`IVehicleControlScheme`) を通って `VehicleMotorInput` になり、`SetInput` に届く。
   - `VehicleMotorInput` は -1 から 1 の軸を 8 本 (`AxisCount`) 持つだけで、軸に意味は無い。どの番号の軸を何に使うかはモーターが決め、番号を定数で出す (`CarDrive.ThrottleAxis` など)。
2. `SetInput` では入力を覚えるだけにし、力は `FixedUpdate` で掛ける。座席の入力は `Update` で届くので、その場で掛けると描画の頻度で物理が変わる。
3. 運転席が空くと `VehicleMotorInput.Idle` が届くので、そのときはアクセルなどを放す。
4. 調整値は自分の Spec や ScriptableObject に持たせ、`Configure` などで入れる (`SimpleCarMotor` と `CarSpec` が例)。
5. 最高速度のある動き方は `IVehicleTopSpeed` も実装する。マップの書き出し (`MapVehicleExporter`) が、サーバーの速すぎ判定の上限をここから決める。無ければ既定の上限 (`VehicleDriveSpec.DefaultMaxSpeed`) になる。

### 操作の当て方を決める (`IVehicleControlScheme`)

座席の入力 (`VehicleSeatInput`: 移動・上下・ダッシュ・撃つ・構える・視線の向き) をどの軸に当てるかは、当て方の部品で決める。オフラインの座席もオンラインの運転も同じ当て方を通る。

- 何も付けなければ車の当て方 (`CarControlScheme`) になる。移動の前後が `ThrottleAxis`、左右が `SteerAxis`、ダッシュが `BrakeAxis`、ジャンプ (+1)・しゃがみ (-1) が `RightAxis` (横転を起こす)。
- 変えるなら、`IVehicleControlScheme` を実装した MonoBehaviour を `Vehicle` と同じ GameObject に付ける。コードから入れるなら `VehicleMotorLink.ControlScheme` に入れる (付けた部品より勝つ)。どちらも次の入力から効く。
- 当て方の中では、座席の入力を使っても、自分の `InputAction` を読んでもよい。例: 飛行機なら視線の向きからピッチとロールを出し、移動の前後を推力に当てる。
- 使われる当て方は `VehicleControlSchemes.For(乗り物の GameObject)` で引ける。

### 飛行機 (`SimpleAircraftMotor`)

1. `Rigidbody` と当たり判定を持つ機体の根元に、`Vehicle`・`VehicleMotorLink`・`SimpleAircraftMotor`・`AircraftControlScheme` を付ける。
   - 試走場では西の端 (`VehicleRangeSceneBuilder.AircraftOrigin`) に北向きで置いてあり、smoke の通しで離陸して 15m 昇るまでを確かめる。
2. 飛び方の値は **Create > Shooter > Aircraft Definition** で作ったアセット (`AircraftDefinition`) に置き、`SimpleAircraftMotor` と `AircraftControlScheme` の Definition の欄に付ける。空なら既定値で飛ぶ。コードからは `Configure(AircraftSpec)` でその実行の間だけ差し替えられる。押す力は加速 (m/s²) で持つので、質量を変えても飛び方は変わらない。
   - アセットは質量・推力と、Aero (空力)・Aim (当て方)・Stick (操縦桿)・Ground (地上)・Damage (壊れ方) の欄に分かれる。Stick・Damage はまだ値を持たない。
   - Aero の `MaxSpeed` は水平に全開で飛んだときに落ち着く速さ。降下では重さの分だけ速くなり、`TopSpeed` (`AircraftAero.TopSpeed`) はその速さを返す。
   - `LiftSpeed` より遅いと翼が重さを支えきれずに沈み、`ControlSpeed` より遅いと舵が効きにくくなる (止まっていても `MinControl` だけは効く)。
3. 軸は `AircraftFlight.ThrottleAxis` (0、負はエアブレーキ)・`PitchAxis` (1、+ で機首上げ)・`RollAxis` (2、+ で右へ傾く)・`YawAxis` (3、+ で右へ向く)。
   - 1 ステップの力は、推力 + 空力 (`AircraftAero.Compute`) + 地上の力 (`AircraftGroundContact`) の和。同じ GameObject に `IAircraftGroundSource` を付けると地上の力を、`IAircraftConditionSource` を付けると左右の揚力・舵・推力の効き (0 から 1、`AircraftCondition`) を毎ステップそこから読む。
   - `SimpleAircraftMotor.State` (`AircraftFlightState`) で、対気速度・迎え角・横滑り角 (ラジアン)・荷重倍数 (G)・接地を読める。
   - 地上を走らせるなら、機体の根元に `AircraftWheels` を付け、子に `WheelCollider` を置く。機体の重心より前にある車輪が前輪になり、ヨーで向きが変わる (名前は見ない)。向ける角度は止まっているとき `MaxSteerAngle`、`SteerFadeSpeed` まで速くなるにつれ `HighSpeedSteerAngle` へ絞る (`AircraftGround.SteerLimit`)。
   - ブレーキは全部の車輪に掛かる。エアブレーキ (負のスロットル) の深さに応じて `BrakeTorque` まで、誰も操縦していない (入力が Idle) 間は `IdleBrakeTorque`。浮いている間は前輪をまっすぐに戻し、ブレーキを放す。値はアセットの Ground の欄か `AircraftWheels.Configure(AircraftGroundSpec)` で入れる。
   - `AircraftWheels` が返す `AircraftGroundContact` は、どれかの車輪が接地しているかと、地面からの高さ (`HeightAboveGround`、m) を持つ。高さは浮いているときに一番低い車輪の底から真下へ `HeightProbeDistance` まで測り、地面が無ければ null。機体自身の当たり判定は数えない。
4. `AircraftControlScheme` は、視線の方へ機首を向け、移動の前後でスロットルを開け閉めし (閉じ切ってさらに下げるとエアブレーキ)、左右でロールを足す。強さはアセットの Aim の欄か `Configure(AircraftAimSpec)` で入れる。
   - 乗っている間は視点が機体の向きに付いて回るので、左右の視線のずれは、そのまま曲がり続ける速さになる。上下は視線の高さへ機首が合う。
5. オンラインでは、運転手が降りると機体はサーバーにある最後の姿のまま止まる (車と同じ)。空中で降りると、その場に浮いたまま残る。

プレイヤーの座席の入力を AI・チュートリアル・自動の確認から書き換えるときは、`PlayerVehicleRider.SeatInputFilter` に `IVehicleSeatInputFilter` を入れる。

## 文言・言語 (サンプルの決まり)

キットは言語と文を扱わない。武器などの表示名はキー (ただの文字列) としてデータに持ち、それを文にするのは UI。ここからはサンプルの UI (`Localizer`・`GameText`・`LocalizedTexts`) の決まりで、自分の UI を作るなら従わなくてよい。

文言は、サンプルの `Resources/Strings/<言語コード>.json` の後に `Resources/Localization/<言語コード>/*.json` をファイル名の順に重ね、同じキーは後のファイルが勝つ。`Resources/ShooterKit/` の `LocalizationContent` (**Create > Shooter > Localization Content**) に言語ごとの表を入れると、さらにその後に重なる。
利用側のファイルは、サンプルのどのファイルより後に並ぶ名前 (`zz-mygame.json` など) にする。同じ名前だとどちらが読まれるか決まらない。

- 文を変える: `Assets/Resources/Localization/ja/zz-mygame.json` に、変えるキーだけを書く。
- 言語を足す: `Assets/Resources/Localization/Languages/zz-mygame.json` に言語を書き、`Assets/Resources/Localization/<言語コード>/` に `ja` と同じキーの表を置く。表が 1 つでもある言語が設定画面で選べるようになる。

  ```json
  { "languages": [ { "code": "fr", "name": "Français", "systemLanguages": ["French"] } ] }
  ```

- 選んだ言語: 設定画面で選ぶと `LanguagePreferenceStore` が `PlayerPrefs` の `Shooter.Language` に言語コードで保存する。キットの操作設定 (`ControlSettings`) には入れない。保存が無ければ OS の言語から決め (言語の一覧の `systemLanguages` に合う言語、どれにも合わなければ `otherSystemLanguages`)、前の形式 (操作設定の中の `language`) が残っていれば最初に読んだときに `Shooter.Language` へ移す。
