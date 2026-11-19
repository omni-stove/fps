# Shooter Kit (`com.omnistove.shooter-kit`)

一人称・三人称のシューターのゲームの進行 (ルール・戦闘・移動・乗り物・ネット対戦) だけを持つ UPM パッケージ。
画面・文言・見た目・音・武器やルールの中身は持たない。各ゲームはキットの口に合わせて、それらを自分で実装する。
サンプル **Demo Game** (`Samples~/Demo`) は、その実装例の一つ。

項目・型・値は README に書かず、次の正本を見る。

| 知りたいこと | 正本 |
| --- | --- |
| Unity の版・依存パッケージ | `package.json` |
| コマンドと引数 | `mise tasks` (`.mise/tasks.toml`) |
| 武器・ルールのデータの項目 | `Shooter.Core` の Spec |
| 調整値の項目と既定値 | `Resources/Tuning/*.json` |
| アセット・コンポーネントの欄 | 各型の Inspector の Tooltip |
| UI へ出る状態 | `Scripts/Runtime/GameSignals.cs` のプロパティと、その型 |
| EOS の設定の名前 | `Scripts/Core/EosCredentials.cs` |
| 専用サーバー | `docs/dedicated-server.md` |

## 導入

1. `Packages/manifest.json` の `dependencies` に足す。`#` の後ろはタグか SHA にし、`Packages/packages-lock.json` もコミットする。

   ```json
   "com.omnistove.shooter-kit": "https://github.com/omni-stove/shooter-kit.git?path=/Assets/game#v0.1.0"
   ```

2. **Edit > Project Settings > Player > Active Input Handling** を `Input System Package (New)` か `Both` にする。
3. テストも回すなら、manifest に `"testables": ["com.omnistove.shooter-kit"]` を足す。

EOS (`Shooter.NetEos`) は `com.playeveryware.eos` を入れたときだけコンパイルされる。

## 各ゲームが実装するもの

| 実装するもの | キットの口 |
| --- | --- |
| 武器・ルール・調整値・見た目と音の参照 | 入口のアセット `GameContent` |
| 画面 (HUD・メニュー・ポーズ) と文言 | 状態は `GameSignals`、出来事は `CombatEvents` から読み、操作の口を `GameUi` に入れる |
| 乗り物の動き方と操作の当て方 | `IVehicleMotor`・`IVehicleControlScheme` |
| クラウドセーブの保存先・サインイン | `ICloudSaveBackend`・`IAccountBackend` |

### データ

入口のアセット `GameContent` を `make game-content` (か **Shooter > Create Game Content**) で作り、欄に自分のデータを入れる。入れ忘れは `make audit-replacements` で点検する。

### 画面

`GameSignals` の項目は `GameSignal<T>` で、今の値 `Value`・値があるか `HasValue`・変わったときの `Changed` を持つ。購読を始めたら `Value` を読んで合わせる。モードごとの状態は `ModeStatus.Detail` に入る。

```csharp
private void OnEnable()
{
    GameSignals.HudModel.Changed += Bind;
    Bind(GameSignals.HudModel.Value);
}

private void OnDisable()
{
    GameSignals.HudModel.Changed -= Bind;
}
```

モードの状態は `GameSignals.ModeStatus` の `Detail` に届き、ネット対戦にあるモードもオフラインと同じ型で届く。画面は `Detail` の型を見て何を出すかを決める (Demo は型ごとの書式役を `ModeHudFormatters` に登録している)。

| モード | `Detail` の型 |
| --- | --- |
| チームデスマッチ | `ModeSession` |
| Free for All | `FreeForAllStanding` |
| Kill Confirmed | `KillConfirmed` |
| Hardpoint | `Hardpoint` |
| Search and Destroy | `SearchAndDestroyStatus` |
| Capture the Flag | `CaptureTheFlagStatus` |
| Payload | `PayloadStatus` |
| Execution | `ExecutionStatus` |
| Guardian | `GuardianStatus` |
| Gun Game | `GunGameStatus` |
| Battle Royale | `BattleRoyaleStatus` (物資投下の印・復帰までの秒と残り回数も入る) |
| Horde | `HordeStatus` |
| Infected | `InfectedStatus` |
| Extraction | `ExtractionStatus` |

### 機能ごとの口

調整値の項目と既定値は `Resources/Tuning/*.json`、欄の意味は各型の Tooltip を見る。入口の `GameContent` に JSON を入れればそちらを読む。

#### UI の拡大と色覚

- UI の拡大は操作設定の `ControlSettings.UiScale` (1 = 100%) にある。キットは画面を持たないので、ゲームが自分の `PanelSettings.scale` に掛け、`AppliedControlSettings.Changed` で掛け直す (Demo は `DemoPanelSettings`)。
- 色覚のパレットは `ControlSettings.Palette` で選び、色は `TeamColors.Current` (`TeamPalette`) から引く。味方・敵・中立・強調・目的・危険・競合・解除済みの色があり、
  見る側のチームは `GameSignals.PaletteViewer` に届く。色を見分けにくい人向けのパレットでは `UsesMarks` が立つので、`MarkOf` の印も添えて色だけに頼らない。
  キットが描く物 (爆弾・設置場所・縮むエリアの境界) はすでにパレットを通している。

#### Payload

- シーンに `PayloadRunner` と `PayloadCart` を置く。台車の道筋の点・中継点・範囲は `PayloadCart` に、攻め側と倒れてから戻るまでの秒は `PayloadRunner` に入れる。
  速さ・押す人数ごとの倍率・押されないときの戻り方・中継点で足す時間・持ち時間は `payload.json` から読む。`PayloadDefinition` のアセットを入れれば、これらをまとめてそちらから読む。
- 台車の見た目は `PayloadCart` に Collider 付きの物を入れる。空なら箱を作る。
- 専用サーバーで遊ぶには、`make export-server-rules` が台車の道筋をマップの地点ファイルに書き出す (`docs/dedicated-server.md`)。

#### Battle Royale の物資と復帰

- 物資投下は `BattleRoyaleRunner` の `SupplyDropper` が、縮むエリアの段階ごとに、次の円の中へ決めた数と間隔で箱を落とす。数・間隔・中身の表・開ける距離は `supply-drops.json` に、
  箱の見た目 (開ける前・開けた後) は `SupplyDropper` に入れる。空なら仮の箱を作る。
- 復帰は `redeploy.json` の段階 (`lastPhaseIndex`) までに倒された人を、残り回数 (`charges`) がある間、`delaySeconds` 後にパラシュートで戻す。
- 画面は `BattleRoyaleStatus` の物資投下の印と、自分の復帰までの秒・残り回数を出す。

#### 購入経済

- Search and Destroy のラウンドに人ごとの財布を付ける。額は `economy.json` の財布の欄に、買える品と値段は同じファイルの `items` に入れる (Horde のチームで分け合う通貨とは別物)。
- 画面は `GameSignals.Wallet` (`WalletStatus`: 所持金・上限・買える時間か) を出し、`GameSignals.BuySender` (`IBuySender.Buy`) で買い、
  答えは `CombatEvents.PurchaseAnswered` で受ける (断った理由も入る)。オフラインでもネット対戦でも同じ口で、ネット対戦では額をルールの JSON の `match.economy` から読む。

#### ミューテーターとプレイリスト

- ミューテーターはルールの一部を上に重ねたもの (`GameRulesJson.Parse(text, sourceName, baseRules)` と同じ書き方) で、`mutators.json` に Id ごとに置き、`GameRulesOverride.PushMutator` で積む。
- プレイリストはモード・マップ (シーン)・ルール名・ミューテーターの並びで、`playlists.json` に置く。オフラインでは `PlaylistRunner.Begin` で始め、試合が終わるたびに次のシーンへ進む。
  専用サーバーは同じ書き方のファイルを `-playlist` で読み、試合の合間にモードとルールを替える。

#### 暗視・サーマル

- 照準の `SightSpec.visionMode` (`Night`・`Thermal`) は覗いている間だけ効く。ゴーグルは `VisionGogglesDefinition` (アビリティ) で、アビリティのキーで掛け外しする。
  どちらも操作するプレイヤーの `VisionModeController` が決め、今の見え方を `GameSignals.Vision` (`VisionStatus`) に出す。
- 画面は `GameSignals.Vision` を見て覆いを描く。覆いの画像と色・入れ切りの音・サーマルで敵に貼る材質は `GameContent` の vision のカタログ (`VisionVisualCatalog`) から取り、
  空なら色だけで覆い、材質はコードで作る。オフラインだけで、ネット対戦では効かない。

#### ドローン

- `DroneDeployDefinition` (置ける物のアビリティ) を持たせる。置くとその場から操縦を始め、近くの敵をミニマップと印に出す。置いている間にアビリティを短く押すと戻す。
- 操縦の状態 (操縦しているか・電池と離れられる距離の残り・見つけた敵の数) は `GameSignals.Drone` (`DroneStatus`) に出る。見た目は置ける物の見た目に入れ、空なら胴と羽の箱を作る。
- オフラインだけ。専用サーバーは置く要求を断る。

#### 盾

- 武器のデータの部品 `shield` (`RiotShieldSpec`、Inspector では `WeaponDefinition` の盾の欄) で、武器を盾にする。持っている間は前に盾を出して弾を止め、撃てなくなり、攻撃のボタンで盾で殴る。
- 盾の見た目と当たり判定は盾の欄の Prefab (根に Collider) に入れる。空なら幅と高さの板を作る。
- オフラインだけ。ネット対戦のルールの武器には入れない。

### 乗り物

`IVehicleMotor` を実装した MonoBehaviour を、`Vehicle`・`VehicleMotorLink` と同じ GameObject に付ける。操作の当て方を替えるなら、`IVehicleControlScheme` の実装も付ける。

### 保存先とサインイン

`ICloudSaveBackend` を `CloudSaveBootstrap.Backend` に、`IAccountBackend` を `AccountBootstrap.Backend` に、`SubsystemRegistration` の初期化で入れる。入れなければ EOS を使い、EOS も無ければ手元だけで動く。

クラウドへ同期するのは `CloudSaveBootstrap.RegisterSlots(owns, startupSlots, canRead)` で登録したスロットだけ。自分のセーブも同期するなら、`SubsystemRegistration` の初期化で、どのスロットが自分のものか・起動時に見に行くスロット・クラウドの中身を読めるか (読めなければ `false` か `FormatException`) を登録する (プロフィールは `ProgressionCloudSave`、マッチングのレートは `SkillCloudSave`、キャンペーンは `CampaignCloudSave` が登録する)。

マッチングのレートは進行のプロフィールではなく本体の `SkillRecorder` がスロット `skill` に持ち、`SkillBootstrap.Current.Rating` で読む。進行を外したゲームでもレートは回る。版 5 までのプロフィールにあったレートは、`skill` が無いときだけ `ProgressionRecorder` が読み込んだときに引き継ぐ。

装備などの解放は `Unlocks.IsUnlocked(id)` で問い合わせる。門 (`Unlocks.Install(gate)`) が無ければすべて解放で、進行 (`ProgressionBootstrap`) を入れるとプロフィールのレベルで決まる。別の決め方を使うなら自分の門を入れ、外すときは `Unlocks.Uninstall(gate)` に同じ門を渡す。

## Demo サンプル

1. **Window > Package Manager** で Shooter Kit の **Samples** から **Demo Game** を **Import** する。
2. **Shooter > Build All Scenes** を一度実行する（Editor を閉じて `make build-scenes` でもよい。`make setup` も最後にこれを実行する）。全シーンを作り直し、ネット対戦のマップの当たりも書き出す。

このリポジトリでは `Assets/Samples/ShooterKitDemo` が `Assets/game/Samples~/Demo` へのシンボリックリンクなので、サンプルは `Samples~/Demo` の下を直す。
