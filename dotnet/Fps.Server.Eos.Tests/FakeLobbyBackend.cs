using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Server.Eos.Tests
{
    /// <summary>要求を記録し、テストから完了させる <see cref="IServerLobbyBackend"/>。</summary>
    internal sealed class FakeLobbyBackend : IServerLobbyBackend
    {
        public event Action<string, string> Removed;

        public event Action<string> LoggedOut;

        public int Ticks { get; private set; }

        public int Logins { get; private set; }

        public int Creates { get; private set; }

        public string CreatedBucket { get; private set; }

        public int CreatedMaxMembers { get; private set; }

        public IReadOnlyList<LobbyAttribute> CreatedAttributes { get; private set; }

        public string UpdatedLobbyId { get; private set; }

        public List<IReadOnlyList<LobbyAttribute>> Updates { get; } = new List<IReadOnlyList<LobbyAttribute>>();

        public List<string> Destroys { get; } = new List<string>();

        public bool Disposed { get; private set; }

        public Action<string, string> PendingLogin { get; private set; }

        public Action<LobbyInfo, string> PendingCreate { get; private set; }

        public Action<string> PendingUpdate { get; private set; }

        public Action<string> PendingDestroy { get; private set; }

        /// <summary>Tick のたびに呼ぶ。SDK が Tick の中で完了を知らせるのをまねる。</summary>
        public Action OnTick { get; set; }

        public void Tick()
        {
            Ticks++;
            OnTick?.Invoke();
        }

        public void Login(Action<string, string> done)
        {
            Logins++;
            PendingLogin = done;
        }

        public void CreateLobby(string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            Creates++;
            CreatedBucket = bucketId;
            CreatedMaxMembers = maxMembers;
            CreatedAttributes = attributes;
            PendingCreate = done;
        }

        public void UpdateAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
        {
            UpdatedLobbyId = lobbyId;
            Updates.Add(attributes);
            PendingUpdate = done;
        }

        public void DestroyLobby(string lobbyId, Action<string> done)
        {
            Destroys.Add(lobbyId);
            PendingDestroy = done;
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

        public void CompleteUpdate(string error = null)
        {
            Take(PendingUpdate, () => PendingUpdate = null)(error);
        }

        public void CompleteDestroy(string error = null)
        {
            Take(PendingDestroy, () => PendingDestroy = null)(error);
        }

        public void RaiseLoggedOut(string reason)
        {
            LoggedOut?.Invoke(reason);
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
