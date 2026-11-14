using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Net;
using Fps.NetV2;
using Fps.Protocol;

namespace Fps.NetEos
{
    /// <summary>
    /// <c>-netstack v2 -transport eos</c> のクライアントの通信路を、PlayEveryWare のプラグインの P2P (<see cref="EosClientP2P"/>) で作る。
    /// 認証情報は Netcode のクライアントと同じ場所 (<see cref="EosCredentialSource"/>) から読む。
    /// </summary>
    internal sealed class EosClientTransportFactory : IEosClientTransportFactory
    {
        public bool TryLoadCredentials(out EosCredentials credentials, out IReadOnlyList<string> missing, out string error)
        {
            return EosCredentialSource.TryLoad(out credentials, out missing, out error);
        }

        public IMatchTransport Create(EosCredentials credentials, Action<string> log)
        {
            return new EosMatchTransport(new EosClientP2P(credentials), new EosTransportOptions(), log);
        }
    }
}
