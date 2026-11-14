using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Server.Eos
{
    /// <summary>
    /// SDK の型を外に出さず文字列と <see cref="LobbyInfo"/> で受け渡すのは、SDK の無いビルドとテストでも
    /// <see cref="ServerLobbyRegistrar"/> を動かせるようにするため。
    /// </summary>
    /// <remarks>
    /// 完了のコールバックは要求ごとに 1 回だけ、<see cref="Tick"/> の中か、すぐ失敗が分かったときは呼び出しの中で呼ぶ。
    /// 成功なら (結果, null)、失敗なら (null, 理由) を渡す。<see cref="IDisposable.Dispose"/> の後はコールバックを呼ばない。
    /// </remarks>
    internal interface IServerLobbyBackend : IDisposable
    {
        /// <summary>引数はロビー ID と理由。</summary>
        event Action<string, string> Removed;

        /// <summary>参加中のロビーがあれば、先に <see cref="Removed"/> を呼ぶ。</summary>
        event Action<string> LoggedOut;

        void Tick();

        void Login(Action<string, string> done);

        void CreateLobby(string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done);

        void UpdateAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        void DestroyLobby(string lobbyId, Action<string> done);
    }
}
