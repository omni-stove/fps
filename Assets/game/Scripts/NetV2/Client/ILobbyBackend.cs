using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は Shooter.NetEos.EosLobbyBackend に置き、この口で QuickMatchSearch に渡す。
    public interface ILobbyBackend : IDisposable
    {
        event Action<string> LoggedOut;

        event Action<string, string> MemberAttributesChanged;

        void Tick();

        void Login(Action<string, string> done);

        void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done);

        void CreateMemberLobby(
            string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done);

        void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done);

        void LeaveMemberLobby(string lobbyId, Action<string> done);

        void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done);

        void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done);

        void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done);

        void LeaveMatchLobby(string lobbyId, Action<string> done);
    }
}
