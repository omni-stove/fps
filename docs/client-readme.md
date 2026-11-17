# shooter

この zip はゲームのクライアントです。Release の Assets から自分の OS の zip をダウンロードし、展開して起動します。

| OS | zip | 起動するもの |
| --- | --- | --- |
| Windows 10 / 11 (64 bit) | `shooter-<版>-windows.zip` | 展開したフォルダの `shooter.exe` |
| macOS 12 以降 | `shooter-<版>-macos.zip` | 展開したフォルダの `shooter.app` |

版は起動時のログの `[Version]` の行に出ます。

## Windows

1. zip を右クリックして「すべて展開」を選びます (zip の中から直接起動すると `shooter_Data` が読めずに起動しません)。
2. 展開したフォルダの `shooter.exe` をダブルクリックします。
3. 「Windows によって PC が保護されました」と出たら、「詳細情報」を押してから「実行」を押します。署名していないので最初の 1 回だけ出ます。

フォルダの中のファイル (`shooter_Data` や `UnityPlayer.dll` など) は `shooter.exe` と同じ場所に置いたままにしてください。

## macOS

このアプリは Apple の署名と公証 (notarization) をしていないので、ダブルクリックでは「開発元を検証できないため開けません」と出て開けません。次の手順で開きます。

1. zip をダブルクリックして展開し、`shooter.app` を「アプリケーション」など好きな場所へ移します。
2. Finder で `shooter.app` を **右クリック (または control キーを押しながらクリック)** し、「開く」を選びます。
3. 確認のダイアログでもう一度「開く」を押します。2 回目からはダブルクリックで開けます。

macOS 15 (Sequoia) 以降で右クリックの「開く」が出ないときは、一度ダブルクリックしてダイアログを閉じたあと、
「システム設定」→「プライバシーとセキュリティ」の下の方にある「このまま開く」を押し、続くダイアログで「開く」を押します。

それでも「壊れているため開けません」と出るときは、ターミナルで次を実行してから開き直します (`shooter.app` の場所は自分の環境に合わせます)。

```sh
xattr -dr com.apple.quarantine /Applications/shooter.app
codesign --force --deep --sign - /Applications/shooter.app
```

1 行目はダウンロードした印 (quarantine) を外し、2 行目はこの Mac の上でだけ通る署名 (ad-hoc 署名) を付け直します。

## オンラインで遊ぶ

メインメニューの「ONLINE - QUICK MATCH」を押すと、空きのあるサーバーを探してつなぎます。サーバーのアドレスを知っているときは、
起動するものに引数を付けるとそのサーバーへつなぎます。

```sh
shooter.exe -mode client -address 203.0.113.10 -port 7777     # Windows
shooter.app/Contents/MacOS/shooter -mode client -address 203.0.113.10 -port 7777     # macOS
```

サーバーとの接続が切れると、理由を出してメインメニューに戻ります。もう一度クイックマッチを押せばつなぎ直せます。
