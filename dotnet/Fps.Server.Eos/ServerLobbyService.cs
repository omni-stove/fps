using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;

namespace Fps.Server.Eos
{
    /// <summary>
    /// Unity の <c>Fps.Net.LobbyService</c> のうち、サーバーのロビーの登録に使う部分を移したもの。ログの行も同じにする。
    /// 検索と参加を持たないのは、サーバーは自分のロビーを作るだけで、他のロビーに入らないため。
    /// </summary>
    internal sealed class ServerLobbyService : IDisposable
    {
        public const string LogPrefix = "[Lobby] ";

        private readonly IServerLobbyBackend _backend;
        private readonly string _bucketId;
        private readonly TextWriter _log;
        private readonly List<Action<string>> _leaveWaiters = new List<Action<string>>();
        private Operation _operation;
        private bool _leaveWhenSettled;
        private bool _updating;
        private bool _disposed;

        public ServerLobbyService(IServerLobbyBackend backend, string bucketId, TextWriter log)
        {
            if (string.IsNullOrWhiteSpace(bucketId))
            {
                throw new ArgumentException("must not be null or blank", nameof(bucketId));
            }

            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _bucketId = bucketId;
            _backend.Removed += HandleRemoved;
            _backend.LoggedOut += HandleLoggedOut;
        }

        /// <summary>引数は理由。自分で出たときは呼ばない。</summary>
        public event Action<string> Removed;

        /// <summary>ロビーに入っていれば先に <see cref="Removed"/> を呼ぶ。</summary>
        public event Action<string> LoggedOut;

        private enum Operation
        {
            None,
            Login,
            Create,
            Leave,
        }

        public string LocalUserId { get; private set; }

        public LobbyInfo Current { get; private set; }

        public bool IsLoggedIn => LocalUserId != null;

        /// <summary>属性の更新は含めない (<see cref="IsUpdating"/>)。</summary>
        public bool IsBusy => _operation != Operation.None;

        public bool IsUpdating => _updating;

        public void Tick()
        {
            if (!_disposed)
            {
                _backend.Tick();
            }
        }

        public void Login(Action<string, string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            ThrowIfUnusable();
            if (IsLoggedIn)
            {
                throw new InvalidOperationException("already logged in as " + LocalUserId);
            }

            _operation = Operation.Login;
            _backend.Login(Once<string>((userId, error) =>
            {
                _operation = Operation.None;
                if (error == null && string.IsNullOrWhiteSpace(userId))
                {
                    error = "the lobby service returned no user id";
                }

                if (error != null)
                {
                    done(null, error);
                    return;
                }

                LocalUserId = userId;
                _log.WriteLine(LogPrefix + "logged in with the device id user=" + userId);
                done(userId, null);
            }));
        }

        public void CreateLobby(int maxPlayers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            if (maxPlayers < LobbyLaunchOptions.MinMaxPlayers || maxPlayers > LobbyLaunchOptions.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers,
                    "must be between " + LobbyLaunchOptions.MinMaxPlayers + " and " + LobbyLaunchOptions.MaxMaxPlayers);
            }

            var copied = CopyAttributes(attributes, nameof(attributes));
            ThrowIfUnusable();
            if (!IsLoggedIn)
            {
                throw new InvalidOperationException("log in before using lobbies");
            }

            if (Current != null)
            {
                throw new InvalidOperationException("already in lobby " + Current.Id + "; leave it first");
            }

            _operation = Operation.Create;
            _backend.CreateLobby(_bucketId, maxPlayers, copied, Once<LobbyInfo>((lobby, error) =>
            {
                _operation = Operation.None;
                if (error == null && lobby == null)
                {
                    error = "the lobby service returned no lobby";
                }

                if (error != null)
                {
                    _leaveWhenSettled = false;
                    FlushLeaveWaiters(null);
                    done(null, error);
                    return;
                }

                Current = lobby;
                _log.WriteLine(LogPrefix + "created id=" + lobby.Id + " max=" + lobby.MaxMembers + " owner=" + lobby.OwnerUserId
                    + (copied.Length == 0 ? "" : " " + Describe(copied)));
                _log.WriteLine(LogPrefix + "members=" + lobby.DescribeMembers() + " id=" + lobby.Id);
                if (!_leaveWhenSettled)
                {
                    done(lobby, null);
                    return;
                }

                _leaveWhenSettled = false;
                LeaveLobby(FlushLeaveWaiters);
                done(null, "the lobby was left while it was being created");
            }));
        }

        /// <summary>更新は 1 つずつ行い、ほかの操作とは並べて進められる。</summary>
        public void UpdateAttributes(IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            var copied = CopyAttributes(attributes, nameof(attributes));
            if (copied.Length == 0)
            {
                throw new ArgumentException("must contain at least one attribute", nameof(attributes));
            }

            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_updating)
            {
                throw new InvalidOperationException("an attribute update is still in progress");
            }

            if (Current == null)
            {
                throw new InvalidOperationException("not in a lobby");
            }

            var lobbyId = Current.Id;
            _updating = true;
            _backend.UpdateAttributes(lobbyId, copied, Once((string error) =>
            {
                _updating = false;
                _log.WriteLine(error != null
                    ? LogPrefix + "could not update id=" + lobbyId + " " + Describe(copied) + ": " + error
                    : LogPrefix + "updated id=" + lobbyId + " " + Describe(copied));
                done(error);
            }));
        }

        /// <summary>
        /// 作ったロビーを閉じる。作成の途中なら、作り終えたところで閉じる。入っていなければ何もせず成功にする。
        /// いつ呼んでも例外にしない。
        /// </summary>
        public void LeaveLobby(Action<string> done = null)
        {
            if (_disposed)
            {
                done?.Invoke(null);
                return;
            }

            if (_operation == Operation.Create || _operation == Operation.Leave)
            {
                // 作り終えて実際に閉じるまで完了にしない。先に完了にすると、呼び出し側が閉じ終えたと誤解する
                _leaveWhenSettled |= _operation == Operation.Create;
                if (done != null)
                {
                    _leaveWaiters.Add(done);
                }

                return;
            }

            if (Current == null)
            {
                done?.Invoke(null);
                return;
            }

            var lobby = Current;
            Current = null;
            _operation = Operation.Leave;
            _backend.DestroyLobby(lobby.Id, Once((string error) =>
            {
                _operation = Operation.None;
                _log.WriteLine(error != null
                    ? LogPrefix + "could not leave id=" + lobby.Id + ": " + error
                    : LogPrefix + "closed id=" + lobby.Id);
                done?.Invoke(error);
                FlushLeaveWaiters(error);
            }));
        }

        /// <summary>ロビーに入っていれば閉じる要求を出し、バックエンドを閉じる。以後のコールバックは呼ばない。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_operation == Operation.None && Current != null)
            {
                LeaveLobby();
            }

            _disposed = true;
            _backend.Removed -= HandleRemoved;
            _backend.LoggedOut -= HandleLoggedOut;
            _backend.Dispose();
            Current = null;
            Removed = null;
            LoggedOut = null;
        }

        private void FlushLeaveWaiters(string error)
        {
            if (_leaveWaiters.Count == 0)
            {
                return;
            }

            var waiters = _leaveWaiters.ToArray();
            _leaveWaiters.Clear();
            foreach (var waiter in waiters)
            {
                waiter(error);
            }
        }

        private void HandleRemoved(string lobbyId, string reason)
        {
            if (_disposed || Current == null || !string.Equals(lobbyId, Current.Id, StringComparison.Ordinal))
            {
                return;
            }

            Current = null;
            _log.WriteLine(LogPrefix + "removed from id=" + lobbyId + ": " + reason);
            Removed?.Invoke(reason);
        }

        private void HandleLoggedOut(string reason)
        {
            if (_disposed || !IsLoggedIn)
            {
                return;
            }

            // バックエンドが先にロビーからの退出を知らせなかったときも、ログインの無いロビーには留まれない
            if (Current != null)
            {
                HandleRemoved(Current.Id, "logged out: " + reason);
            }

            if (_disposed)
            {
                return;
            }

            _log.WriteLine(LogPrefix + "logged out user=" + LocalUserId + ": " + reason);
            LocalUserId = null;
            LoggedOut?.Invoke(reason);
        }

        private static LobbyAttribute[] CopyAttributes(IReadOnlyList<LobbyAttribute> attributes, string paramName)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(paramName);
            }

            var copied = new LobbyAttribute[attributes.Count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copied.Length; i++)
            {
                var attribute = attributes[i] ?? throw new ArgumentException("must not contain null", paramName);
                if (!keys.Add(attribute.Key))
                {
                    throw new ArgumentException("key '" + attribute.Key + "' is given more than once", paramName);
                }

                copied[i] = attribute;
            }

            return copied;
        }

        private static string Describe(IEnumerable<LobbyAttribute> attributes)
        {
            var parts = new List<string>();
            foreach (var attribute in attributes)
            {
                parts.Add(attribute.ToString());
            }

            return string.Join(" ", parts);
        }

        private void ThrowIfUnusable()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_operation != Operation.None)
            {
                throw new InvalidOperationException(_operation + " is still in progress");
            }
        }

        private Action<T, string> Once<T>(Action<T, string> handler)
        {
            var called = false;
            return (result, error) =>
            {
                if (called || _disposed)
                {
                    return;
                }

                called = true;
                handler(result, error);
            };
        }

        private Action<string> Once(Action<string> handler)
        {
            var called = false;
            return error =>
            {
                if (called || _disposed)
                {
                    return;
                }

                called = true;
                handler(error);
            };
        }
    }
}
