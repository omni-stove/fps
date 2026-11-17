# Easy Anti-Cheat (EAC)

専用サーバーは EOS の Anti-Cheat Server、クライアントは Anti-Cheat Client の ClientServer モードを使う。
サーバーはクライアントを ProductUserId で登録するので、EAC は `-transport eos` のときだけ使える。

承認の直後に互いの有効・無効を伝え合い、食い違えば切る (守られたクライアントと `-noeac` のクライアントを同じ試合に入れない)。

## 有効・無効

サーバーは `-eac` で有効になる。クライアントはエディタの外で `-transport eos` のとき有効で、`-noeac` で外せる。
有効なクライアントをブートストラッパーを通さずに起動すると試合に入らずに終わるので、開発ビルドを直接 EOS で試すときは `-noeac` を付ける。
判定の結果は起動時の `[AntiCheat]` の行に出る。

## ポータルでの設定 (人が行う)

1. [Epic Developer Portal](https://dev.epicgames.com/portal) の **Game Services → Anti-Cheat** で Anti-Cheat を有効にする。
2. 使う Sandbox / Deployment で Client protection と **Client-server** を有効にする。
3. 配布するプラットフォームを足し、Anti-Cheat の SDK (ブートストラッパー・整合性ツール・署名の鍵) を取る。鍵と証明書はリポジトリに入れない。

## 配布物を作る (ビルドごと)

1. `make build` でクライアントをビルドする。
2. 整合性ツール (`anticheat_integritytool`) を出力フォルダに対して実行し、整合性カタログを作る。
3. ブートストラッパー (`start_protected_game`) と `EasyAntiCheat/` (`Settings.json` に Product ID・Sandbox ID・Deployment ID と実行ファイル名・タイトル) を実行ファイルの隣に置く。
4. プレイヤーには EAC サービスのインストーラを一度管理者権限で実行してもらい (ストアならストアの設定で入れる)、ブートストラッパーから起動してもらう。

## 確かめる

サーバーとクライアントは別の PC か別の OS ユーザーで動かす (EOS の Device ID が端末・ユーザーごとに 1 つのため)。

1. `make eos-sdk` の後に `make server-dotnet ARGS="-transport eos -eac"` で起動し、ログの `[Net] EOS server ready id=<ID>` を控える。
2. 保護したクライアントをブートストラッパーから `-mode client -transport eos -eos-server <ID>` で起動し、1 試合遊べることを確かめる。
3. 次の組み合わせでは、クライアントが断られてメニューへ戻る。
   - `-noeac` のクライアント → `-eac` のサーバー
   - 保護したクライアント → `-eac` の無いサーバー

EOS の無い手元では `dotnet/Shooter.NetV2.Client.Tests/AntiCheatConnectionTests.cs` が UDP の上で同じ流れを確かめる。
