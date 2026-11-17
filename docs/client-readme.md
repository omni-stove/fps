# shooter

Release の Assets から自分の OS の zip をダウンロードします。

## Windows 10 / 11

1. `shooter-<版>-windows.zip` を右クリックして「すべて展開」を選びます。zip の中からは起動できません。
2. `shooter.exe` をダブルクリックします。フォルダの中のほかのファイルは同じ場所に置いたままにします。
3. 「Windows によって PC が保護されました」と出たら、「詳細情報」→「実行」を押します (初回だけ)。

## macOS 12 以降

1. `shooter-<版>-macos.zip` を展開し、`shooter.app` を「アプリケーション」へ移します。
2. `shooter.app` を **control キーを押しながらクリック** し、「開く」を選び、もう一度「開く」を押します (初回だけ)。

「開く」が出ないとき (macOS 15 以降): 一度ダブルクリックして閉じ、「システム設定」→「プライバシーとセキュリティ」の「このまま開く」を押します。

「壊れているため開けません」と出るとき: ターミナルで次を実行してから開き直します。

```sh
xattr -dr com.apple.quarantine /Applications/shooter.app
codesign --force --deep --sign - /Applications/shooter.app
```

## オンラインで遊ぶ

メインメニューの「ONLINE - QUICK MATCH」を押します。接続が切れたらメニューに戻るので、もう一度押します。
