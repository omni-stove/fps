using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ネットワークが動いている (サーバー・ホスト・クライアントのどれかとして待ち受けか接続をしている) 間は、
    /// <see cref="TimeScaleArbiter"/> にネット対戦中と答え、時間の速さの要求を断らせる。
    /// </summary>
    public static class NetTimeScaleGuard
    {
        /// <summary>ネットワークが動いているか。</summary>
        public static bool IsNetworkRunning()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening;
        }

        // TimeScaleArbiter は SubsystemRegistration で登録を消すので、それより後に入れる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            TimeScaleArbiter.NetworkSessionSource = IsNetworkRunning;
        }
    }
}
