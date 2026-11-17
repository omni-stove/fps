# クライアントのビルドと配布

## 手元でビルドする

```sh
make build                 # macOS
make build windows
make build webgl
```

出力先と版の決まり方は `mise tasks`、入れるシーンは `BuildSceneList` にある。
Unity Hub で対象の Build Support (Windows / Mac は Mono、WebGL は Web) を入れておく。

## Release に添付する

`v*` のタグを push すると、Windows と macOS の zip が Release に付く。
zip と Release の本文には `docs/client-readme.md` が入る。`-` を含むタグは pre-release になる。

```sh
git tag v0.2.0 && git push origin v0.2.0
```

試しに出したら消す。

```sh
git tag v0.0.0-test && git push origin v0.0.0-test
gh release delete v0.0.0-test --cleanup-tag --yes && git tag -d v0.0.0-test
```

## Web 版 (GitHub Pages)

`v*` のタグか、Actions の **Web demo** を手で走らせると Pages に置かれる。

- 初めに一度、**Settings > Pages > Build and deployment > Source** を **GitHub Actions** にする。
- ブラウザでは UDP が使えないので、オンライン対戦は無い。
