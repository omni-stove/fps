using Fps.NetV2;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// 起動時に、クライアントの EOS の通信路とクイックマッチのロビーの作り方 (<see cref="EosClientTransportFactory"/>) を
    /// <see cref="NetV2Eos"/> へ登録する。Fps.NetEos は com.playeveryware.eos があるときだけコンパイルされる。
    /// 無ければ何も登録されず、クライアントは EOS を選ばれたときに「このビルドに無い」というエラーにする。
    /// </summary>
    public static class EosTransportInstaller
    {
        /// <summary>初めて Device ID でログインしたときに作るユーザーの表示名。</summary>
        public const string LobbyDisplayName = "fps-player";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            NetV2Eos.Factory = new EosClientTransportFactory();
        }
    }
}
