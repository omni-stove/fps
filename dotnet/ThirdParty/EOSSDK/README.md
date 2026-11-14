# EOS の C# SDK

.NET のサーバー (`dotnet/Fps.Server`) が EOS を直接呼ぶための SDK を置く場所。
SDK は Epic Online Services Developer Agreement の下で配られるので、リポジトリにはコミットしない。
このフォルダの中身は README.md を除いて `.gitignore` で除いてあり、各自が手元に置く。

SDK が無くてもビルドとテストは通る。そのときサーバーは起動時に `[Eos] sdk=missing ...` と出し、EOS の機能は使えない。

## 置き方

```
dotnet/ThirdParty/EOSSDK/
  Source/            SDK の C# のソース (.cs)。下の階層はそのままでよい
  Bin/<RID>/         その RID 向けのネイティブのライブラリ
    linux-x64/       libEOSSDK-Linux-Shipping.so
    linux-arm64/     libEOSSDK-LinuxArm64-Shipping.so
    osx-arm64/       libEOSSDK-Mac-Shipping.dylib
    osx-x64/         libEOSSDK-Mac-Shipping.dylib
    win-x64/         EOSSDK-Win64-Shipping.dll
    win-arm64/       EOSSDK-Win64-Shippingarm64.dll
  license.txt        (任意) SDK に付いてくる配布の条件
```

使う RID の分だけ置けばよい。ビルドは `-r <RID>` を付ければその RID の、付けなければビルドするマシンの RID のライブラリを出力先に写す。
ファイル名は SDK に入っているままでよい (Linux の arm64 版は、SDK が読みに行く `libEOSSDK-Linux-Shipping.so` の名前で写される)。

SDK の版は決めていない。クライアントの PlayEveryWare のプラグインと同じ版にしておくと、両者の挙動の違いを疑わずに済む。

## 入手先

どちらかから取る。

- **PlayEveryWare の EOS プラグイン**: Unity でこのプロジェクトを一度開くと `Library/PackageCache/com.playeveryware.eos@*/` に入る。
  `Runtime/EOS_SDK/` が C# のソース、`Runtime/Linux/`・`Runtime/macOS/`・`Runtime/Windows/x64/` などがライブラリ。Linux の arm64 版は入っていない。
- **Epic Developer Portal** (https://dev.epicgames.com/portal): SDK のダウンロードから **EOS SDK (C#)** の zip を落とす。
  zip の `SDK/Source/` が C# のソース、`SDK/Bin/` がライブラリ (Linux の arm64 版を含む)。

`make eos-sdk` でどちらからでも写せる。置き直すたびに `Source/` と `Bin/` を作り直す。

```sh
make eos-sdk                                         # Library/PackageCache の PlayEveryWare のプラグインから
make eos-sdk ARGS="--from ~/Downloads/EOS-SDK.zip"   # ポータルの zip (か、それを展開したフォルダ) から
```

置いたら `make test` か `make server-dotnet` で確かめる。サーバーが `[Eos] sdk=included library=... version=...` と出せば SDK とライブラリの両方が読めている。

## 別の場所に置く

`EosSdkDir` (環境変数か `dotnet build -p:EosSdkDir=<path>`) で、このフォルダの代わりに使う場所を指せる。中の形は上と同じ。

## SDK を使うコードを書く

`dotnet/EosSdk.props` を読み込んだプロジェクトは、SDK があるときだけ `EosSdkAvailable` が `true` になり、`EOS_SDK` とその OS の `EOS_PLATFORM_*` が定義される。
SDK の型を使うコードは `#if EOS_SDK` で囲むか、`Condition="'$(EosSdkAvailable)' == 'true'"` を付けた項目でだけコンパイルする。
SDK そのものは `dotnet/Fps.EosSdk` に入る。参照すればネイティブのライブラリも出力先に写る。`Fps.EosSdk.EosSdkBuild.Included` でビルドに SDK が入っているかを確かめられる。
