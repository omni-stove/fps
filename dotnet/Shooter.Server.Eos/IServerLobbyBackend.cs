using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server.Eos
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

        /// <param name="bucketId">ロビーを載せるバケット。</param>
        /// <param name="maxMembers">オーナーを含むメンバーの上限。</param>
        /// <param name="voiceRoom">RTC 部屋を開き、入ったメンバーがボイスで話せるようにするか。</param>
        /// <param name="attributes">作った直後に付ける公開の属性。</param>
        /// <param name="done">作ったロビーか、失敗の理由を受け取る。</param>
        void CreateLobby(string bucketId, int maxMembers, bool voiceRoom, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done);

        void UpdateAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        void DestroyLobby(string lobbyId, Action<string> done);
    }
}
