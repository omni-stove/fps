using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // MemberLobbies を SDK 抜きで動かし、テストで偽の Lobby インターフェースに差し替えられるよう、SDK の呼び出しを 1 つずつここに分ける。
    // ロビーが無い・もう入っていないときの理由は LobbyBackendErrors.NotFound にする。MemberLobbies はそれを「もう出ている」として扱うため。
    // LeaveLobby は使う側が捨てられた後も受け付ける。捨てるときに入ったままのロビーから出るため。
    public interface IMemberLobbyApi
    {
        event Action<string, string> MemberUpdateReceived;

        void CreateLobby(string bucketId, int maxMembers, Action<string, string> done);

        void UpdateLobbyAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        void UpdateMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        void JoinLobby(string lobbyId, Action<string> done);

        void LeaveLobby(string lobbyId, Action<string> done);

        LobbyInfo CopyLobby(string lobbyId, out string reason);

        IReadOnlyList<LobbyAttribute> CopyMemberAttributes(string lobbyId, string userId, out string reason);
    }
}
