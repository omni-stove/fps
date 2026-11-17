using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server.Eos
{
    // SDK の型を外に出さず文字列と LobbyInfo で受け渡すのは、SDK の無いビルドとテストでも
    // ServerLobbyRegistrar を動かせるようにするため。
    internal interface IServerLobbyBackend : IDisposable
    {
        event Action<string, string> Removed;

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
