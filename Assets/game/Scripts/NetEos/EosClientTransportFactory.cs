using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.NetV2;
using Fps.Protocol;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// クライアントの EOS の通信路を PlayEveryWare のプラグインの P2P (<see cref="EosClientP2P"/>) で、クイックマッチのロビーの検索を
    /// <see cref="EosLobbyBackend"/> で作る。認証情報は環境変数と、作業ディレクトリか実行ファイルの隣の
    /// <see cref="EosCredentials.LocalFileName"/> から読む (<see cref="EosCredentialSource"/>)。
    /// </summary>
    internal sealed class EosClientTransportFactory : IEosClientTransportFactory
    {
        public bool TryLoadCredentials(out EosCredentials credentials, out IReadOnlyList<string> missing, out string error)
        {
            var paths = EosCredentialSource.CandidatePaths(Directory.GetCurrentDirectory(), Path.GetDirectoryName(Application.dataPath));
            return EosCredentialSource.TryLoad(Environment.GetEnvironmentVariable, paths, out credentials, out missing, out error);
        }

        public IMatchTransport Create(EosCredentials credentials, Action<string> log)
        {
            return new EosMatchTransport(new EosClientP2P(credentials), new EosTransportOptions(), log);
        }

        public ILobbyBackend CreateLobbyBackend(EosCredentials credentials)
        {
            return new EosLobbyBackend(credentials, EosTransportInstaller.LobbyDisplayName);
        }
    }
}
