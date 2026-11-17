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

ゲーム進行は `GameSignals` に値を出すだけで、HUD はサンプルの既定の UI (`Fps.Runtime.UI`) が作る。サンプルを取り込まなければ HUD 無しで動く。差し替えるなら、シーンを読む前に次を済ませる。

- `DefaultHudFactory.Enabled = false` で既定の HUD を止め、`GameSignals` を購読して自分の HUD を作る。
- `GameUi.Pause`・`LoadoutSelect`・`Menu` に自分の実装を入れる。既定の HUD を止めたら `GameUi.Pause` は必須 (無いとポーズ中も操作が止まらない)。

## 文言・言語

文言は `Resources/Localization/<言語コード>/*.json` をファイル名の順に重ね、同じキーは後のファイルが勝つ。
利用側のファイルは、サンプルのどのファイルより後に並ぶ名前 (`zz-mygame.json` など) にする。同じ名前だとどちらが読まれるか決まらない。

- 文を変える: `Assets/Resources/Localization/ja/zz-mygame.json` に、変えるキーだけを書く。
- 言語を足す: `Assets/Resources/Localization/Languages/zz-mygame.json` に言語を書き、`Assets/Resources/Localization/<言語コード>/` に `ja` と同じキーの表を置く。

  ```json
  { "languages": [ { "code": "fr", "name": "Français", "systemLanguages": ["French"] } ] }
  ```
