using Fps.Net;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// 起動時に <see cref="EosP2PTransport"/> の作り方を <see cref="NetTransportFactories.Eos"/> へ登録する。
    /// このフォルダは asmdef を持たず、自動参照される EOS plugin の SDK と一緒に Assembly-CSharp でコンパイルされる。
    /// </summary>
    public static class EosTransportInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            NetTransportFactories.Eos = (target, options, credentials) =>
            {
                var transport = target.AddComponent<EosP2PTransport>();
                transport.Configure(options, credentials);
                return transport;
            };
        }
    }
}
