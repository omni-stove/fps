# コメントの整理 (#936)

`.cs` のコメントを AGENTS.md の「なぜ別の書き方をしないか」だけにする手順。

## 判定

塊ごとに keep・delete・rewrite (説明と理由が混ざったものを、理由だけの `//` の文にする) を決める。
迷ったら、消したときに次に読む人が素直な書き方に直してしまうかで決める。

## 手順

ファイル全体は読まず、判断に要る塊だけ前後を読む。

1. 抜き出す: `mkdir -p Temp/comments && make comments-extract Assets/game/Scripts/Runtime/Mode > Temp/comments/mode.txt`
2. 判定ファイルを `Temp/` に書く。keep は省いてよい。rewrite は続く行に本文 (`//` 無し) を書き、`@@end` で閉じる。

   ```text
   Assets/game/Scripts/Core/Weapons/Ammo/HeatAmmo.cs:28-28:231d2551 delete
   Assets/game/Scripts/Core/Progression.cs:40-43:0a1b2c3d rewrite
   値を覚えずに呼ぶたびに今のプロフィールを引くのは、試合中にレベルが上がればその場で使えるようにするため。
   @@end
   ```

3. 書き戻す: `make comments-apply Temp/comments/mode-decisions.txt`。抽出後に変わった塊はエラーで残るので、抜き出し直す。
4. 確かめる: `make comments-verify` と `make lint`。使われなくなった `using` は消してよい。
