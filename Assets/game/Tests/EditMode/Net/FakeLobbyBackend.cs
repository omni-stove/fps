using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Net.Tests
{
    /// <summary>要求を記録し、テストから完了させる <see cref="ILobbyBackend"/>。</summary>
    internal sealed class FakeLobbyBackend : ILobbyBackend
    {
        public event Action<LobbyInfo> MembersChanged;

        public event Action<string, string> Removed;

        public int Ticks { get; private set; }

        public bool Disposed { get; private set; }

        public Action<string, string> PendingLogin { get; private set; }

        public Action<LobbyInfo, string> PendingCreate { get; private set; }

        public int CreatedMaxMembers { get; private set; }

        public string CreatedBucket { get; private set; }

        public Action<IReadOnlyList<LobbyInfo>, string> PendingSearch { get; private set; }

        public int Searches { get; private set; }

        public string SearchedBucket { get; private set; }

        public Action<LobbyInfo, string> PendingJoin { get; private set; }

        public string JoinedId { get; private set; }

        public Action<string> PendingLeave { get; private set; }

        public List<string> Leaves { get; } = new List<string>();

        public void Tick()
        {
            Ticks++;
        }

        public void Login(Action<string, string> done)
        {
            PendingLogin = done;
        }

        public void CreateLobby(string bucketId, int maxMembers, Action<LobbyInfo, string> done)
        {
            CreatedBucket = bucketId;
            CreatedMaxMembers = maxMembers;
            PendingCreate = done;
        }

        public void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done)
        {
            Searches++;
            SearchedBucket = bucketId;
            PendingSearch = done;
        }

        public void JoinLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            JoinedId = lobbyId;
            PendingJoin = done;
        }

        public void LeaveLobby(string lobbyId, bool destroy, Action<string> done)
        {
            Leaves.Add((destroy ? "destroy:" : "leave:") + lobbyId);
            PendingLeave = done;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void CompleteLogin(string userId, string error = null)
        {
            Take(PendingLogin, () => PendingLogin = null)(userId, error);
        }

        public void CompleteCreate(LobbyInfo lobby, string error = null)
        {
            Take(PendingCreate, () => PendingCreate = null)(lobby, error);
        }

        public void CompleteSearch(IReadOnlyList<LobbyInfo> lobbies, string error = null)
        {
            Take(PendingSearch, () => PendingSearch = null)(lobbies, error);
        }

        public void CompleteJoin(LobbyInfo lobby, string error = null)
        {
            Take(PendingJoin, () => PendingJoin = null)(lobby, error);
        }

        public void CompleteLeave(string error = null)
        {
            Take(PendingLeave, () => PendingLeave = null)(error);
        }

        public void RaiseMembersChanged(LobbyInfo lobby)
        {
            MembersChanged?.Invoke(lobby);
        }

        public void RaiseRemoved(string lobbyId, string reason)
        {
            Removed?.Invoke(lobbyId, reason);
        }

        private static T Take<T>(T pending, Action clear)
            where T : Delegate
        {
            if (pending == null)
            {
                throw new InvalidOperationException("no pending " + typeof(T).Name);
            }

            clear();
            return pending;
        }
    }
}
