# Easy Anti-Cheat (EAC)

.NET の専用サーバー (`dotnet/Fps.Server`) が EOS の **Anti-Cheat Server インターフェース**、ゲームのクライアントが
Anti-Cheat Client の **ClientServer モード** を使う。ポータルでの設定と配布物の用意は人が行う。

## 仕組み

- コード: サーバーは `dotnet/Fps.Server/ServerAntiCheat.cs` (Hello・登録・中継・切断) と
  `dotnet/Fps.Server.Eos/EosAntiCheatServerBackend.cs` (SDK の呼び出し。EOS の C# SDK を置いたビルドだけに入る)。
  クライアントは `Assets/game/Scripts/NetV2/Client/ClientAntiCheat.cs` (Hello と中継)、`Assets/game/Scripts/NetV2/NetV2AntiCheat.cs`
  (SDK の側を差し込む口)、`Assets/game/Scripts/NetEos/EosAntiCheatClientSession.cs` (PlayEveryWare のプラグインの SDK の呼び出し)。
  有効・無効の判定とパケットの形は `Assets/game/Scripts/Core/AntiCheat*.cs`。
- EAC のパケットは `Fps.Protocol` の `AntiCheatToServerMessage` / `AntiCheatToClientMessage` に `AntiCheatPacket` をそのまま載せ、
  試合のメッセージと同じ通信路の確実な届け方で送る。サーバーが承認した直後から送り合う。
- 承認の直後に互いの EAC の有効・無効を Hello で伝える。片方だけ有効なら切断する (`-noeac` の相手と守られた相手を同じ試合に入れない)。
  Hello が 15 秒来なければ切断する。サーバーが切るときはクライアントへ `fps:rejected:anti-cheat` を送り、クライアントは
  「サーバーに断られました」の文でメニューへ戻る。クライアントが先に食い違いに気付いたときも、自分の Hello がサーバーへ届くよう
  1 秒待ってから同じ文で抜ける。
- サーバーはクライアントを ProductUserId で EAC に登録するので、相手の ID を確かめられる EOS の transport (`-transport eos`) が要る。
  transport は `Fps.Protocol.IConnectionUserIds` を実装して ID を渡す。

## 有効・無効の決まり方

### クライアント

上から順に判定する。結果は起動時に `[AntiCheat] ...` の 1 行でログに出る。

| 条件 | 結果 |
| --- | --- |
| `-noeac` と `-eac` を両方付けた | 引数エラーで終了 (`[AntiCheat] invalid arguments`、終了コード 2) |
| `-noeac` | 無効 (`[AntiCheat] disabled by -noeac`) |
| transport が UDP (既定、クイックマッチ、または EOS の認証情報が無くて UDP に切り替えた) | 無効 |
| エディタで `-eac` が無い | 無効 (エディタでは既定で無効) |
| それ以外 (プレイヤーの `-mode client -transport eos`) | 有効 (`[AntiCheat] enabled (client-server, client)`) |

有効なのに EAC のセッションを始められないとき (ブートストラッパーを通さずに起動したなど) は、
`[AntiCheat] ...; launch the game through the EAC bootstrapper (start_protected_game) or pass -noeac` を出して終わる
(batchmode では終了コード 6)。守られないまま試合に入らない。

開発ビルドを直接起動して EOS で試すときは `-noeac` を付ける。

### サーバー

サーバーの EAC は既定で無効 (`[AntiCheat] disabled on the dedicated server by default; ...`)。`-eac` を付けると有効になる
(`-transport eos` と EOS の認証情報、EOS の C# SDK とネイティブのライブラリが要る)。

| サーバーの引数 | 結果 |
| --- | --- |
| `-noeac` と `-eac` を両方付けた | 引数エラーで終了 (終了コード 2) |
| `-eac` で `-transport eos` でない | 引数エラーで終了 (`-eac needs -transport eos ...`、終了コード 2) |
| `-eac` で認証情報が無い・SDK が無い | 設定エラーで終了 (`[AntiCheat] -eac needs EOS credentials ...` / `[AntiCheat] -eac cannot use EOS: ...`、終了コード 3) |
| `-eac` で `BeginSession` に失敗した | 起動に失敗して終了 (`[Server] failed to start: [AntiCheat] anti-cheat BeginSession failed: ...`、終了コード 1) |
| `-eac` | 有効 (`[AntiCheat] enabled (client-server, dedicated server)`) |
| それ以外 | 無効。EAC が無効なクライアントだけが遊べる |

## ポータルでの設定 (人が行う)

1. [Epic Developer Portal](https://dev.epicgames.com/portal) で、この製品の **Game Services → Anti-Cheat** を開き、
   Anti-Cheat を有効にする (利用規約への同意が要る)。
2. 使う Sandbox / Deployment で、クライアント保護 (Client protection) と **Client-server** (サーバーの保護) の利用を有効にする。
3. 配布するプラットフォーム (Windows / macOS / Linux) を追加し、Anti-Cheat の SDK (ブートストラッパーと
   整合性ツール、署名用の鍵) をダウンロードする。鍵と証明書はリポジトリに入れない。
4. EOS の認証情報 (`EOS_PRODUCT_ID` など) は環境変数か `eos.local.json` で渡す。

## 配布物の用意

ポータルからダウンロードした Anti-Cheat SDK に含まれるツールで、ビルドごとに行う。

1. `make build` でクライアントをビルドする。
2. 整合性ツール (`anticheat_integritytool`) をビルドの出力フォルダに対して実行し、整合性カタログを作る。
   ゲームのファイルを変えたら作り直す。
3. ブートストラッパー (`start_protected_game`) と `EasyAntiCheat/` フォルダ (設定ファイル `Settings.json` を含む)
   をゲームの実行ファイルと同じ階層に置く。`Settings.json` には Product ID / Sandbox ID / Deployment ID と、
   起動するゲームの実行ファイル名・タイトルを書く。
4. 初回はブートストラッパーと一緒に配布される EAC サービスのインストーラを管理者権限で実行する
   (ストアで配布する場合はストア側の設定でインストールさせる)。
5. プレイヤーにはブートストラッパーから起動してもらう。起動引数はブートストラッパーに付ければゲームへ渡る。

## 有効にしたときの確認手順

サーバーとクライアントは別の PC か別の OS ユーザーで動かす (EOS の Device ID が端末・ユーザーごとに 1 つなので)。

1. EOS の認証情報を `eos.local.json` か環境変数で渡し、`make eos-sdk` で SDK を置いてから `make server-dotnet ARGS="-transport eos -eac"`
   (または `-transport eos -eac` を付けて `dotnet/Fps.Server` かサーバーのイメージを起動) する。ログに次が出る。
   - `[AntiCheat] enabled (client-server, dedicated server)`
   - `[AntiCheat] session started (client-server)`
   - `[Net] EOS server ready id=<サーバーの ID> ...`
2. 上の手順で保護したクライアントを、ブートストラッパーから `-mode client -transport eos -eos-server <サーバーの ID>` で起動する。
   - クライアント: `[AntiCheat] enabled (client-server, client)`、`[AntiCheat] session started (client-server)`、
     `[AntiCheat] server id=0 runs anti-cheat; relaying its messages`
   - サーバー: `[AntiCheat] registered peer id=<接続の番号> user=<クライアントの ID> handle=<n>`、しばらくして
     `[AntiCheat] peer id=... auth status=RemoteAuthComplete`
   - そのまま 1 試合遊べる。
3. 食い違いの確認: クライアントを `-noeac` 付きで (ブートストラッパーを通さずに) つなぐと、サーバーに
   `[AntiCheat] removing peer id=...: the peer runs without anti-cheat (-noeac or an unprotected build)`、
   クライアントに `[AntiCheat] removing peer id=0: the peer requires anti-cheat but this side runs without it` が出て、
   クライアントは断られた文でメニューへ戻る。
4. `-eac` を付けずに起動したサーバーに、保護したクライアントでつなぐと、サーバーに
   `[AntiCheat] removing peer id=...: the peer requires anti-cheat but this side runs without it`、
   クライアントに `[AntiCheat] removing peer id=0: the peer runs without anti-cheat (-noeac or an unprotected build)` が出て断られる。
5. 未保護の確認: 保護していないクライアントを `-noeac` 無しで直接起動して `-eac` のサーバーにつなぐと、承認の直後に
   `[AntiCheat] anti-cheat BeginSession failed: ...; launch the game through the EAC bootstrapper ...` が出て終わる
   (batchmode では終了コード 6)。

EOS の認証情報も SDK も無い手元では、両側の SDK を差し替えたテスト (`dotnet/Fps.NetV2.Client.Tests/AntiCheatConnectionTests.cs`) が
UDP の上で同じ Hello・中継・切断を確かめる。
