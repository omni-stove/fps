# Easy Anti-Cheat (EAC)

EOS の Anti-Cheat Client インターフェースを **PeerToPeer モード** で使い、ホスト型 (`-mode host` と `-mode client`) の
EOS P2P 接続に組み込んでいる。ポータルでの設定と配布物の用意は人が行う。

## 仕組み

- コード: `Assets/game/Scripts/NetEos/EosAntiCheat.cs` (SDK の呼び出し)、
  `Assets/game/Scripts/Core/AntiCheat*.cs` (有効・無効の判定、ピアの状態、パケット形式)。
- ホストは接続してきたクライアント全員を、クライアントはホストだけを EAC にピアとして登録する
  (クライアント同士はホストを通してしかやりとりしないため)。
- EAC が相手へ送るよう求めたメッセージは、EOS P2P の同じソケットの **チャネル 3** (EAC 専用) で相手へ中継し、
  相手から届いたものを `ReceiveMessageFromPeer` で EAC へ渡す。Netcode のデータとは混ぜない。
- 接続直後に互いの EAC の有効・無効を Hello パケットで伝える。片方だけ有効なら切断する (`-noeac` の相手と
  守られた相手を同じ試合に入れない)。Hello が 15 秒来なければ切断する。
- EAC が `RemovePlayer` を求めたら、ホストはそのクライアントを切断し、クライアントはホストとの接続を終える。
- 自分の整合性違反 (`ClientIntegrityViolated`) を受けたら transport を止める (`[Net] transport failure`)。
- 専用サーバー (`-mode server`) は守られたピアにならないので EAC を使わない。EAC が有効なクライアントは
  Hello の食い違いで切断されるので、専用サーバーにつなぐクライアントは `-noeac` を付ける。
  専用サーバー対応 (Anti-Cheat Server インターフェース) は未実装。

## 有効・無効の決まり方

上から順に判定する。結果は起動時に `[AntiCheat] ...` の 1 行でログに出る。

| 条件 | 結果 |
| --- | --- |
| `-noeac` と `-eac` を両方付けた | 引数エラーで終了 (`[AntiCheat] invalid arguments`) |
| `-noeac` | 無効 (`[AntiCheat] disabled by -noeac`) |
| transport が UDP (既定、または EOS の認証情報が無くて UDP に切り替えた) | 無効 |
| `-mode server` | 無効 |
| エディタで `-eac` が無い | 無効 (エディタでは既定で無効) |
| それ以外 (プレイヤーの `-transport eos` の host / client) | 有効 |

有効なのに EAC のセッションを始められないとき (ブートストラッパーを通さずに起動したなど) は、
`[AntiCheat] ...; launch the game through the EAC bootstrapper (start_protected_game) or pass -noeac` を出して
transport を止める。守られないまま試合に入らない。

開発ビルドを直接起動して EOS で試すときは `-noeac` を付ける (相手も `-noeac`)。

## ポータルでの設定 (人が行う)

1. [Epic Developer Portal](https://dev.epicgames.com/portal) で、この製品の **Game Services → Anti-Cheat** を開き、
   Anti-Cheat を有効にする (利用規約への同意が要る)。
2. 使う Sandbox / Deployment で、クライアント保護 (Client protection) と **Peer-to-peer** の利用を有効にする。
3. 配布するプラットフォーム (Windows / macOS / Linux) を追加し、Anti-Cheat の SDK (ブートストラッパーと
   整合性ツール、署名用の鍵) をダウンロードする。鍵と証明書はリポジトリに入れない。
4. EOS の認証情報 (`EOS_PRODUCT_ID` など) は今と同じく環境変数か `eos.local.json` で渡す。

## 配布物の用意

ポータルからダウンロードした Anti-Cheat SDK に含まれるツールで、ビルドごとに行う。

1. `make build` で通常のプレイヤーをビルドする (専用サーバービルドは EAC を使わないので対象外)。
2. 整合性ツール (`anticheat_integritytool`) をビルドの出力フォルダに対して実行し、整合性カタログを作る。
   ゲームのファイルを変えたら作り直す。
3. ブートストラッパー (`start_protected_game`) と `EasyAntiCheat/` フォルダ (設定ファイル `Settings.json` を含む)
   をゲームの実行ファイルと同じ階層に置く。`Settings.json` には Product ID / Sandbox ID / Deployment ID と、
   起動するゲームの実行ファイル名・タイトルを書く。
4. 初回はブートストラッパーと一緒に配布される EAC サービスのインストーラを管理者権限で実行する
   (ストアで配布する場合はストア側の設定でインストールさせる)。
5. プレイヤーにはブートストラッパーから起動してもらう。起動引数はブートストラッパーに付ければゲームへ渡る。

## 有効にしたときの確認手順

2 台の PC (または同じ PC の別の OS ユーザー。EOS の Device ID が端末・ユーザーごとに 1 つなので) で行う。

1. 上の手順で保護したビルドを用意し、ホスト側でブートストラッパーから
   `-mode host -transport eos` を付けて起動する。ログに次が出る。
   - `[AntiCheat] enabled (peer-to-peer, host)`
   - `[AntiCheat] session started (peer-to-peer) user=<ホストの ID>`
   - `[Net] EOS server ready id=<ホストの ID> ...`
2. クライアント側でブートストラッパーから `-mode client -transport eos -eos-server <ホストの ID>` を付けて起動する。
   双方のログに次が出る。
   - `[AntiCheat] registered peer id=<相手の transport ID> user=<相手の ID> handle=<n>`
   - しばらくして `[AntiCheat] peer id=... auth status=RemoteAuthComplete`
3. 食い違いの確認: クライアントを `-noeac` 付きで (ブートストラッパーを通さずに) 起動すると、ホストに
   `[AntiCheat] removing peer id=...: the peer runs without anti-cheat (-noeac or an unprotected build)`、
   クライアントに `[AntiCheat] removing peer id=0: the peer requires anti-cheat but this side runs without it` が出て切断される。
4. 未保護の確認: ブートストラッパーを通さずに `-mode host -transport eos` で起動すると、
   `[AntiCheat] anti-cheat BeginSession failed: ...; launch the game through the EAC bootstrapper ...` が出て
   `[Net] transport failure` で終わる (batchmode では終了コード 6)。

EAC を使わないときの確認: エディタで NetTest を再生する、またはプレイヤーを引数なし・`-transport udp`・
`-noeac` 付きで起動すると `[AntiCheat] disabled ...` が出て、これまでどおり遊べる。
