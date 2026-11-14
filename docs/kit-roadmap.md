# キットとしての方針

このリポジトリはシューターのキットで、各ゲームは素材と設定を渡して差し替えることで出来上がる。
今入っている武器・ルール・シーン・見た目・音・数値は動作を確かめるための仮のもので、ゲームごとに置き換わる前提で扱う。

この文書は、キットとゲームの境目をどこに引くかと、そこへ寄せていく順番を決める。
大きな移行 (ファイルの移動やデータへの切り出し) は並行中の作業と衝突しやすいので、ここに方針だけ置き、
落ち着いた時期にまとめて進める。それまでの間は「新しく書くコードの決まり」に従い、移行の量を増やさない。

## 値の 3 分類

コードやデータにある値・素材は、次のどれかに分ける。

| 分類 | 意味 | 置き場所 | 例 |
| --- | --- | --- | --- |
| ゲームの調整値 | ゲームごとに変えるもの | データ (JSON / ScriptableObject)。コードには既定値だけ持つ | 武器の性能、ルール、移動速度、敵 AI の視界や反応、グレネード、各モードの得点や時間 |
| キットの内部値 | 実装の都合で決まり、ゲームが触る理由のないもの | コードの定数のまま | `NetworkPlayerMotor.ReconcileTolerance`・`TickRate`、`EnemyAgent.RepathDistance`、バッファの大きさ |
| サンプルの中身 | 動作確認用の仮の素材・データ・シーン | サンプル側 (後述の分離のあと) | `Weapons/Json/*.json`、`Rules/Json/*.json`、Campaign01、`*SceneBuilder` が作るシーン、`PlayerMockBody` |

迷ったら「別のゲームを作る人がこの値を変えたくなるか」で決める。変えたくなるなら調整値。

## 今の状態

### できていること

- パッケージ: `Assets/game` が UPM パッケージ `com.omnistove.fps` になっていて、ゲームのプロジェクトから
  `?path=/Assets/game` で入れられる。シーンやアセットを作るスクリプトは出力先ルート (`-fpsKitRoot`) の下に書く
  (`Assets/game/README.md`)。
- 武器: `Assets/game/Weapons/Json/*.json` → `WeaponSpec`。表示名は表のキー (`nameKey`)。
- ルール: `Assets/game/Rules/Json/*.json` → `GameRules`。
- 陣取り系のモード: `DominationDefinition`・`HardpointDefinition` (`Modes/*.asset`、`ModeTuningAssetBuilder` が無ければ既定値で作る) →
  `DominationSpec`・`HardpointSpec`。Runner に配線していなければ Runner の既定値で動く。
- 音とエフェクト: `PresentationCatalog` (Resources の `Presentation/PresentationCatalog`) を差し替えれば変わる。
  武器ごとのものは武器の Id で引く。
- 文言: `Resources/Localization/<言語>/*.json` の表を引く `LocalizedText`。
- 敵 AI の判断: 既定値は `EnemySpec.Default*` (`EnemySpec.Default`)。`EnemyAgent.Definition` に敵のデータ `EnemyDefinition` を設定すると、
  視界・反応・散らばり・遮蔽・種類ごとの値をそこから読む。空なら `EnemyAgent` の Inspector の値 (既定は `EnemySpec.Default*`)。
  JSON から作る取り込みはまだ無い。
- 移動: `MotorSettings` (コンポーネントに割り当てたもの、無ければ Resources の `Movement/MotorSettings`、それも無ければ `MotorSpec.Default`)。
  オフラインの `PlayerController`・ネット対戦の `NetworkPlayerMotor`・`CharacterAnimator` のポーズが同じ値を読む。
- グレネードと印: `Resources/Tuning/grenade.json` → `GrenadeSpec`、`Resources/Tuning/ping.json` → `PingSpec` (`KitTuning`)。
  オフライン (`GrenadeThrower`・`PingController`) とネット対戦 (`NetTeamDeathmatch.GrenadeSpec`・`PingSpec`) が同じ値を読む。
  ファイルが無ければ既定値。
- メニューから移るシーン名: `MenuSceneSettings` (Resources の `Menu/MenuSceneSettings`) を置けば、メインメニュー・射撃場・
  キャンペーン・ネット対戦のシーン名が `MenuRoute` の既定 (MainMenu / Bootstrap / Campaign01 / NetTest) から変わる。
  キャンペーンの選択画面で選ぶミッションのシーンは、これとは別に `MissionCatalog` が持つ。

### まだコードに入っている調整値

`const ... Default*` として持っているもの。データに出し、ゲームが上書きできるようにする。

- 敵 AI の体: `EnemyAgent` の目の高さ・向き直る速さ・しゃがんだ高さ (`DefaultEyeHeight` など)。判断の値と違い `EnemyDefinition` には無い
- 吹き飛ばし: `KnockbackSpec.Default*`。`KnockbackMovement` / `EnemyAgent` の `KnockbackSpec` をコードで差し替えられ、重さだけ Inspector で変えられるが、JSON は無い
- 各モード: `HordeSpec`・`SearchAndDestroySpec`・`KillConfirmedSpec`・`TeamDeathmatchSpec`・`PickupSpec` と、それぞれの `*Runner`。
  オンラインの拠点制圧 (`NetTeamDeathmatch` の `_captureSeconds` など) もまだ `DominationRunner.Default*` を直接持つ
- その他: `DestructiblePiece`・`ShrinkingZone`・`Damageable`・`CameraBoom`・`AdsZoom` の `Default*` はコンポーネントの値
  (オフラインのプレイヤーの体力・三人称のカメラ・覗き込みの秒数は `GameBootstrap` の値) の既定になり、反動の戻り始めまでの秒数は
  武器の JSON (`recoilRecoveryDelaySeconds`) に移った。プレイヤーまわりの値を JSON から読む口はまだ無い

### 設定した値でなく定数を直接見ている箇所

ゲームが値を変えるとずれる。移行を待たずに直してよい (触る範囲が小さい)。

- (移動の値は `MotorSettings` にまとめた。いま知られている箇所は無い)

### コードで作っている仮の見た目

ランタイムで `GameObject.CreatePrimitive` などから形を作っているもの。差し替え口が無い。

`PlayerMockBody`、`GameBootstrap`、`Pickup`、`BombSite`、`GuardianLeaderMarker`、`ShrinkingZone`、
`DestructiblePiece`、`DebrisPool`、`TargetDummy`、`ThrownGrenade`、`ControlPointZone`、`NetPlayer.Downed`

→ `PresentationCatalog` と同じく、カタログにプレハブの枠を設け、空のときだけ仮の形を作る。

### パッケージの中でキット本体とサンプルの境目が無い

パッケージ (`Assets/game`) の中に、コードと並んでサンプルの中身 (`Weapons/Json`、`Rules/Json`、`Missions`、
`Scenes`、`Characters`、`Prefabs`) がそのまま入っている。ゲームのプロジェクトにパッケージを入れると、
仮の中身も必ず一緒に入る。

## 移行の順番

1. **移動の値の統一** (済み。`MotorSettings`)。
2. **パッケージの中でキット本体とサンプルを分ける**。
   - 本体: `Scripts/Core`・`Runtime`・`Runtime/UI`・`Net`・`Net/UI`・`NetEos` と、そのエディタ拡張。
   - サンプル: 武器・ルールの JSON、ミッション、シーン、`*SceneBuilder` などシーンを組むエディタスクリプト、仮の素材。
     パッケージの `Samples~` に移し、`package.json` の `samples` に載せて、ゲーム側が選んで取り込む形にする。
     サンプルの JSON をパッケージの中から読んでいるところ (README の「同梱のサンプル JSON」) も合わせて直す。
   - 移動は `.meta` ごと行い、GUID を変えない。ファイルの移動が多く衝突しやすいので、並行作業の少ない時期にまとめて行う。
   - 本体のテストがサンプルの数値 (Rifle のダメージなど) に頼っていれば、テスト用のデータをテスト側に持たせる。
3. **残りの調整値をデータへ**。敵 AI、各モードの Spec。
4. **仮の見た目の差し替え口**。カタログにプレハブの枠を足す。
5. **拡張点と入口の整理**。
   - 固定の列挙 (`Team` が `Alpha`/`Bravo` の 2 つなど) を、キットとして固定するか開くか決める。
   - ルール・武器・カタログ・キャラクター・文言の表を指す入口のアセットを 1 つ設け、ゲームはそれを差し替える。
   - 差し替え漏れ (武器の Id に演出が無い、文言のキーが無いなど) をまとめて報告する点検を `-executeMethod` で動かせるようにする。
   - `Assets/game/README.md` の「データの差し込み」「UI を差し替える」に並べて、調整値と見た目・音の差し替え方を書く。

## 新しく書くコードの決まり

移行までの間、これ以上移行の量を増やさないために守る。

- ゲームの調整値を新しく足すときは、`const` に直接書かず、既存の JSON / Spec / ScriptableObject に足す。
  無ければ Spec の既定値として持ち、データから上書きできる形にする。
- 調整値を読む側は、定数 (`Default*`) ではなく、設定された値 (Spec やコンポーネントの値) を読む。
- 見た目や音を新しく足すときは、カタログなど差し替えられる参照から引き、コードで作る形は参照が空のときの仮のものに留める。
- 武器・モード・キャラクターなどの中身の名前 (Rifle など) をキット本体のコードに書かない。Id やデータで引く。
