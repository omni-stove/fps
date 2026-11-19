using Shooter.NetV2;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetEos
{
    // NetV2 から直接作らず起動時に NetV2Eos へ登録するのは、Shooter.NetEos が com.playeveryware.eos があるときだけコンパイルされるため。
    // 無ければ何も登録されず、クライアントは EOS を選ばれたときに「このビルドに無い」というエラーにする。クラウドセーブも同じ理由で登録し、無ければ手元だけで保存する。
    public static class EosTransportInstaller
    {
        public const string LobbyDisplayName = "shooter-player";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            var factory = new EosClientTransportFactory();
            NetV2Eos.Factory = factory;
            CloudSaveBootstrap.Backend = new EosPlayerDataStorage(factory);
            AccountBootstrap.Backend = new EosAccountBackend(factory);
        }
    }
}
