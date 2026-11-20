using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 呼び戻しは呼んだその場でも後のフレームでも返してよい。AccountSignIn はどちらでも同じに扱う。
    public interface IAccountBackend
    {
        // 最初の Tick で 1 回だけ呼ぶ。設定が足りないなど、この起動の間は使えないときは false と理由を返す。
        bool TryPrepare(out bool epicAccount, out string unavailable);

        // 今どの手段で入っているか。ロビーなど他の部品が入れたログインも含む。
        AccountKind SignedInKind { get; }

        void Tick();

        void SignInDevice(Action<AccountLoginResult> done);

        // 前に入った Epic アカウントで、画面を出さずに入り直す。覚えていなければ NoSavedLogin で返す。
        void RestoreEpic(Action<AccountLoginResult> done);

        void SignInEpic(Action<AccountLoginResult> done);

        // 次の起動で勝手に入り直さないよう、覚えているサインインも消す。失敗しても今のログインは外して理由を返す。
        void SignOutEpic(Action<string> done);
    }
}
