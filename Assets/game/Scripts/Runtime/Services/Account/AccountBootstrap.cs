using UnityEngine;

namespace Shooter.Runtime
{
    public static class AccountBootstrap
    {
        public const string ObjectName = "Account";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static AccountSignIn _current;

        // Shooter.NetEos はプラグインがあるビルドでだけコンパイルされるので、起動時にそちらから入れてもらう。
        // 無いビルドでは null のまま、サインインの口を出さない。
        public static IAccountBackend Backend { get; set; }

        public static AccountSignIn Current => _current;

        // 起動時の同期に間に合うよう、クラウドセーブの保存先を作る時に呼ばれる。対戦の画面が先に開けばロビーが自分で Device ID で入る。
        public static AccountSignIn Ensure()
        {
            if (_current != null)
            {
                return _current;
            }

            var backend = Backend;
            if (backend == null || !Application.isPlaying)
            {
                return null;
            }

            var signIn = new AccountSignIn(backend);
            Publish(signIn);
            _current = signIn;

            var host = new GameObject(ObjectName);
            Object.DontDestroyOnLoad(host);
            host.AddComponent<AccountDriver>().Bind(signIn);
            return signIn;
        }

        // 使えると分かるまで口を出さないのは、設定が無いビルドでサインインのボタンを見せないため。
        public static void Publish(AccountSignIn signIn)
        {
            signIn.Prepared += () =>
            {
                GameSignals.AccountControl.Publish(signIn);
                GameSignals.Account.Publish(signIn.Status);
            };
            signIn.StatusChanged += status => GameSignals.Account.Publish(status);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _current?.Dispose();
            _current = null;
        }
    }
}
