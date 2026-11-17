using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.NetV2;

namespace Shooter.Net.Tests
{
    internal sealed class FakeLobbyBackend : ILobbyBackend
    {
        private Action<string, string> _login;
        private Action<IReadOnlyList<LobbyInfo>, string> _search;

        public event Action<string> LoggedOut;

        public event Action<string, string> MemberAttributesChanged
        {
            add { }
            remove { }
        }

        public int Ticks { get; private set; }

        public int Logins { get; private set; }

        public int Searches { get; private set; }

        public int Creates { get; private set; }

        public bool Disposed { get; private set; }

        public void Tick()
        {
            Ticks++;
        }

        public void Login(Action<string, string> done)
        {
            Logins++;
            _login = done;
        }

        public void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done)
        {
            Searches++;
            _search = done;
        }

        public void CreateMemberLobby(
            string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            Creates++;
            done(null, LobbyBackendErrors.NotSupported);
        }

        public void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            done(null, LobbyBackendErrors.NotSupported);
        }

        public void LeaveMemberLobby(string lobbyId, Action<string> done)
        {
            done(LobbyBackendErrors.NotSupported);
        }

        public void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            done(null, LobbyBackendErrors.NotSupported);
        }

        public void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
        {
            done(LobbyBackendErrors.NotSupported);
        }

        public void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done)
        {
            done(null, LobbyBackendErrors.NotSupported);
        }

        public bool AcceptsMatchLobbies { get; set; }

        public List<string> MatchLobbyJoins { get; } = new List<string>();

        public void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            MatchLobbyJoins.Add(lobbyId);
            if (!AcceptsMatchLobbies)
            {
                done(null, LobbyBackendErrors.NotSupported);
                return;
            }

            done(new LobbyInfo(lobbyId, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 2, Array.Empty<string>()), null);
        }

        public void LeaveMatchLobby(string lobbyId, Action<string> done)
        {
            done(LobbyBackendErrors.NotSupported);
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void CompleteLogin(string userId, string error = null)
        {
            _login(userId, error);
        }

        public void CompleteSearch(IReadOnlyList<LobbyInfo> lobbies, string error = null)
        {
            _search(lobbies, error);
        }

        public void LogOut(string reason)
        {
            LoggedOut?.Invoke(reason);
        }
    }
}
