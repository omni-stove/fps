using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は Shooter.NetEos.EosLobbyBackend に置き、この口で QuickMatchSearch に渡す。
    public interface ILobbyBackend : IDisposable
    {
        event Action<string> LoggedOut;

        void Tick();

        void Login(Action<string, string> done);

        void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done);
    }
}
