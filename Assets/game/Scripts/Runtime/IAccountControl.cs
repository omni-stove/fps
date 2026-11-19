using Shooter.Core;

namespace Shooter.Runtime
{
    // 画面はこの口からサインインを始め、結果は GameSignals.Account で受け取る。始められないときは false を返す。
    public interface IAccountControl
    {
        AccountStatus Status { get; }

        bool SignInWithEpic();

        bool SignOut();
    }
}
