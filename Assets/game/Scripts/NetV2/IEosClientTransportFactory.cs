using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Shooter.NetV2 が PlayEveryWare のプラグインに依存しないよう、プラグインを使う Shooter.NetEos が実装して NetV2Eos.Factory に入れる。
    public interface IEosClientTransportFactory
    {
        bool TryLoadCredentials(out EosCredentials credentials, out IReadOnlyList<string> missing, out string error);

        IMatchTransport Create(EosCredentials credentials, Action<string> log);

        ILobbyBackend CreateLobbyBackend(EosCredentials credentials);
    }
}
