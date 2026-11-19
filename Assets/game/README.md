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

### 乗り物

`IVehicleMotor` を実装した MonoBehaviour を、`Vehicle`・`VehicleMotorLink` と同じ GameObject に付ける。操作の当て方を替えるなら、`IVehicleControlScheme` の実装も付ける。

### 保存先とサインイン

`ICloudSaveBackend` を `CloudSaveBootstrap.Backend` に、`IAccountBackend` を `AccountBootstrap.Backend` に、`SubsystemRegistration` の初期化で入れる。入れなければ EOS を使い、EOS も無ければ手元だけで動く。

## Demo サンプル

1. **Window > Package Manager** で Shooter Kit の **Samples** から **Demo Game** を **Import** する。
2. **Shooter > Build All Scenes** を一度実行する（Editor を閉じて `make build-scenes` でもよい。`make setup` も最後にこれを実行する）。全シーンを作り直し、ネット対戦のマップの当たりも書き出す。

このリポジトリでは `Assets/Samples/ShooterKitDemo` が `Assets/game/Samples~/Demo` へのシンボリックリンクなので、サンプルは `Samples~/Demo` の下を直す。
