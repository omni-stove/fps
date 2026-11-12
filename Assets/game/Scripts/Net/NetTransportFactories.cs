using System;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// Fps.Net から直接は参照しない transport の作り方を登録しておく場所。
    /// EOS の transport は EOS plugin に依存するので別アセンブリに置き、起動時にここへ自分を登録する
    /// (<c>Fps.NetEos.EosTransportInstaller</c>)。Fps.Net 自体は plugin が無くてもコンパイルできる。
    /// </summary>
    public static class NetTransportFactories
    {
        /// <summary>
        /// EOS P2P の transport を <c>target</c> に追加して返す。未登録 (EOS を含まないビルド) なら null。
        /// 引数は追加先・起動設定 (transport は EOS)・認証情報。
        /// </summary>
        public static Func<GameObject, NetLaunchOptions, EosCredentials, NetworkTransport> Eos { get; set; }
    }
}
