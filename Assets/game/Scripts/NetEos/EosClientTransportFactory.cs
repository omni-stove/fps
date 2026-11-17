using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using UnityEngine;

namespace Shooter.NetEos
{
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

        public IVoiceSession CreateVoiceSession(Action<string> log)
        {
            return new EosVoiceSession(log);
        }
    }
}
