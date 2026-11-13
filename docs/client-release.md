# クライアントのビルドと配布

## 手元でビルドする

Editor を閉じてから次を実行する。版は `--version` で渡すか、無ければ `git describe --tags --always --dirty` から決まる。

```sh
make build                          # macOS → Builds/client-macos/fps.app
make build windows                  # Windows → Builds/client-windows/fps.exe
make build windows ARGS="--version v0.2.0"
```

入れるシーンは `BuildSceneList.Scenes` のうちプロジェクトにあるもので、先頭の `Bootstrap.unity` が起動時のシーンになる。
`Bootstrap.unity` が無ければビルドは失敗し、他に無いシーンは警告を出して飛ばす。出力先のディレクトリはビルドの前に空にする。
Windows 向けには Unity Hub で Windows Build Support (Mono) を、macOS 向けには Mac Build Support (Mono) を入れておく。

## Release に添付する

`v*` のタグを push すると `.github/workflows/client-release.yml` が Windows と macOS のクライアントをビルドし、
`fps-<版>-windows.zip` と `fps-<版>-macos.zip` をそのタグの GitHub Release に添付する (版はタグから先頭の `v` を外したもの)。
専用サーバーのイメージ (`server-image.yml`) も同じタグで作られる。

```sh
git tag v0.2.0
git push origin v0.2.0
```

- `-` を含むタグ (`v0.3.0-rc1` など) は pre-release になる。
- 同じタグを付け直して push すると、Release は作り直さず zip だけを差し替える。
- zip には `docs/client-readme.md` を `README.md` として入れ、Release の本文にも同じものを使う。

### 試しに出す

```sh
git tag v0.0.0-test
git push origin v0.0.0-test
```

Release に zip が 2 つ付いたことを確かめたら、Release とタグを消す。

```sh
gh release delete v0.0.0-test --cleanup-tag --yes
git tag -d v0.0.0-test
```

## macOS の署名と公証

macOS 版は署名も公証もしていない。遊ぶ人は `fps.app` を右クリックして「開く」で起動する (手順は `docs/client-readme.md`)。
