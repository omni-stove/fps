using Shooter.NetV2;
using UnityEngine;

namespace Shooter.NetEos
{
    // NetV2 から直接作らず起動時に NetV2Eos へ登録するのは、Shooter.NetEos が com.playeveryware.eos があるときだけコンパイルされるため。
    // 無ければ何も登録されず、クライアントは EOS を選ばれたときに「このビルドに無い」というエラーにする。
    public static class EosTransportInstaller
    {
        public const string LobbyDisplayName = "shooter-player";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            NetV2Eos.Factory = new EosClientTransportFactory();
        }
    }
}
