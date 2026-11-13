using Fps.Net;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// 起動時に <see cref="EosP2PTransport"/> と <see cref="EosLobbyBackend"/> の作り方を
    /// <see cref="NetTransportFactories"/> へ登録する。
    /// このフォルダは asmdef を持たず、自動参照される EOS plugin の SDK と一緒に Assembly-CSharp でコンパイルされる。
    /// </summary>
    public static class EosTransportInstaller
    {
        /// <summary>初めて Device ID でログインしたときに作るユーザーの表示名。呼び出し側が指定しなければこれを使う。</summary>
        public const string LobbyDisplayName = "fps-player";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            NetTransportFactories.Eos = (target, plan) =>
            {
                var transport = target.AddComponent<EosP2PTransport>();
                transport.Configure(plan.Options, plan.Credentials, plan.AntiCheat);
                return transport;
            };
            NetTransportFactories.EosLobby = (credentials, displayName) =>
                new EosLobbyBackend(credentials, string.IsNullOrWhiteSpace(displayName) ? LobbyDisplayName : displayName);
        }
    }
}
