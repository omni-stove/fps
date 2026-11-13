using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ロビーの作成・検索・参加・退出をまとめる。同時に進める操作は 1 つ、入るロビーも 1 つに限り、
    /// 結果を <c>[Lobby]</c> 付きのログに出す。SDK の呼び出しは <see cref="ILobbyBackend"/> に任せる。
    /// </summary>
    /// <remarks>
    /// 使い方を誤った呼び出し (ログイン前の作成、操作中の別の操作など) は <see cref="InvalidOperationException"/> にし、
    /// サービス側の失敗はコールバックに理由を渡す。<see cref="LeaveLobby"/> だけはいつ呼んでも例外にしない。
    /// </remarks>
    public sealed class LobbyService : IDisposable
    {
        public const string LogPrefix = "[Lobby] ";

        private readonly ILobbyBackend _backend;
        private readonly string _bucketId;
        private Operation _operation;
        private bool _leaveWhenSettled;
        private bool _updating;
        private bool _disposed;

        /// <param name="backend">SDK を呼ぶ側。このサービスが Dispose する。</param>
        /// <param name="bucketId">作る・探すロビーのバケット ID (<see cref="LobbyLaunchOptions.BucketId"/>)。</param>
        public LobbyService(ILobbyBackend backend, string bucketId)
        {
            if (string.IsNullOrWhiteSpace(bucketId))
            {
                throw new ArgumentException("must not be null or blank", nameof(bucketId));
            }

            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _bucketId = bucketId;
            _backend.MembersChanged += HandleMembersChanged;
            _backend.Removed += HandleRemoved;
            _backend.LoggedOut += HandleLoggedOut;
        }

        /// <summary>参加中のロビーのメンバーが変わった。引数は最新の状態。</summary>
        public event Action<LobbyInfo> MembersChanged;

        /// <summary>参加中のロビーから外された (自分で出たときは呼ばない)。引数は理由。</summary>
        public event Action<string> Removed;

        /// <summary>
        /// ログインが切れた。引数は理由。ロビーに入っていれば先に <see cref="Removed"/> を呼ぶ。
        /// この後は <see cref="Login"/> からやり直せる。
        /// </summary>
        public event Action<string> LoggedOut;

        private enum Operation
        {
            None,
            Login,
            Create,
            Search,
            Join,
            Leave,
        }

        /// <summary>ログインした自分の ProductUserId。ログイン前は null。</summary>
        public string LocalUserId { get; private set; }

        /// <summary>参加中のロビー。入っていなければ null。</summary>
        public LobbyInfo Current { get; private set; }

        public bool IsLoggedIn => LocalUserId != null;

        /// <summary>完了を待っている操作があるか。属性の更新は含めない (<see cref="IsUpdating"/>)。</summary>
        public bool IsBusy => _operation != Operation.None;

        /// <summary>属性の更新の完了を待っているか。</summary>
        public bool IsUpdating => _updating;

        /// <summary>参加中のロビーのオーナーが自分か。</summary>
        public bool IsOwner => Current != null && Current.IsOwnedBy(LocalUserId);

        /// <summary>SDK の処理とコールバックを進める。毎フレーム呼ぶ。</summary>
        public void Tick()
        {
            if (!_disposed)
            {
                _backend.Tick();
            }
        }

        /// <summary>Device ID でログインする。成功なら (自分の ProductUserId, null)。</summary>
        public void Login(Action<string, string> done)
        {
            Require(done);
            ThrowIfUnusable();
            if (IsLoggedIn)
            {
                throw new InvalidOperationException("already logged in as " + LocalUserId);
            }

            Begin(Operation.Login);
            _backend.Login(Once<string>((userId, error) =>
            {
                End();
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
                Debug.Log(LogPrefix + "logged in with the device id user=" + userId);
                done(userId, null);
            }));
        }

        /// <summary>最大 <see cref="LobbyLaunchOptions.DefaultMaxPlayers"/> 人のロビーを作る。</summary>
        public void CreateLobby(Action<LobbyInfo, string> done)
        {
            CreateLobby(LobbyLaunchOptions.DefaultMaxPlayers, done);
        }

        /// <summary>ロビーを作ってオーナーとして入る。成功なら (作ったロビー, null)。</summary>
        /// <param name="maxPlayers">
        /// <see cref="LobbyLaunchOptions.MinMaxPlayers"/> から <see cref="LobbyLaunchOptions.MaxMaxPlayers"/> の最大人数。
        /// </param>
        /// <param name="done">完了したときに呼ぶ。</param>
        public void CreateLobby(int maxPlayers, Action<LobbyInfo, string> done)
        {
            CreateLobby(maxPlayers, Array.Empty<LobbyAttribute>(), done);
        }

        /// <summary>属性を付けたロビーを作ってオーナーとして入る。成功なら (作ったロビー, null)。</summary>
        /// <param name="maxPlayers">
        /// <see cref="LobbyLaunchOptions.MinMaxPlayers"/> から <see cref="LobbyLaunchOptions.MaxMaxPlayers"/> の最大人数。
        /// </param>
        /// <param name="attributes">公開する属性。キーの重複は認めない。無ければ空。</param>
        /// <param name="done">完了したときに呼ぶ。</param>
        public void CreateLobby(int maxPlayers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            Require(done);
            if (maxPlayers < LobbyLaunchOptions.MinMaxPlayers || maxPlayers > LobbyLaunchOptions.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers,
                    "must be between " + LobbyLaunchOptions.MinMaxPlayers + " and " + LobbyLaunchOptions.MaxMaxPlayers);
            }

            var copied = CopyAttributes(attributes, nameof(attributes));
            ThrowUnlessReadyOutsideLobby();
            Begin(Operation.Create);
            _backend.CreateLobby(_bucketId, maxPlayers, copied, Once<LobbyInfo>((lobby, error) =>
            {
                End();
                if (!Enter(lobby, ref error))
                {
                    done(null, error);
                    return;
                }

                Debug.Log(LogPrefix + "created id=" + lobby.Id + " max=" + lobby.MaxMembers + " owner=" + lobby.OwnerUserId
                    + (copied.Length == 0 ? "" : " " + Describe(copied)));
                LogMembers(lobby);
                if (!LeaveIfRequested())
                {
                    done(lobby, null);
                    return;
                }

                done(null, "the lobby was left while it was being created");
            }));
        }

        /// <summary>このゲームの公開ロビーを探す。成功なら (見つかったロビー, null)。無ければ空の一覧。</summary>
        public void SearchLobbies(Action<IReadOnlyList<LobbyInfo>, string> done)
        {
            Require(done);
            ThrowUnlessReadyOutsideLobby();
            Begin(Operation.Search);
            _backend.SearchLobbies(_bucketId, LobbyLaunchOptions.MaxSearchResults, Once<IReadOnlyList<LobbyInfo>>((lobbies, error) =>
            {
                End();
                if (error != null)
                {
                    done(null, error);
                    return;
                }

                var found = lobbies ?? Array.Empty<LobbyInfo>();
                Debug.Log(LogPrefix + "found " + found.Count + " lobby(s) in " + _bucketId);
                done(found, null);
            }));
        }

        /// <summary>ID を指定してロビーに入る。成功なら (入ったロビー, null)。</summary>
        public void JoinLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            Require(done);
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                throw new ArgumentException("must not be null or blank", nameof(lobbyId));
            }

            ThrowUnlessReadyOutsideLobby();
            Begin(Operation.Join);
            _backend.JoinLobby(lobbyId, Once<LobbyInfo>((lobby, error) =>
            {
                End();
                if (!Enter(lobby, ref error))
                {
                    done(null, error);
                    return;
                }

                Debug.Log(LogPrefix + "joined id=" + lobby.Id + " owner=" + lobby.OwnerUserId);
                LogMembers(lobby);
                if (!LeaveIfRequested())
                {
                    done(lobby, null);
                    return;
                }

                done(null, "the lobby was left while it was being joined");
            }));
        }

        /// <summary>
        /// オーナーとして参加中のロビーの属性を、同じキーは上書きして足す。成功なら null、失敗なら理由を渡す。
        /// 更新は 1 つずつ行い、ほかの操作とは並べて進められる。
        /// </summary>
        /// <param name="attributes">更新する属性。1 つ以上で、キーの重複は認めない。</param>
        /// <param name="done">完了したときに呼ぶ。</param>
        public void UpdateAttributes(IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
        {
            Require(done);
            var copied = CopyAttributes(attributes, nameof(attributes));
            if (copied.Length == 0)
            {
                throw new ArgumentException("must contain at least one attribute", nameof(attributes));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LobbyService));
            }

            if (_updating)
            {
                throw new InvalidOperationException("an attribute update is still in progress");
            }

            if (!IsOwner)
            {
                throw new InvalidOperationException(Current == null
                    ? "not in a lobby"
                    : "only the owner can update lobby " + Current.Id);
            }

            var lobbyId = Current.Id;
            _updating = true;
            _backend.UpdateAttributes(lobbyId, copied, Once((string error) =>
            {
                _updating = false;
                if (error != null)
                {
                    Debug.LogWarning(LogPrefix + "could not update id=" + lobbyId + " " + Describe(copied) + ": " + error);
                }
                else
                {
                    Debug.Log(LogPrefix + "updated id=" + lobbyId + " " + Describe(copied));
                }

                done(error);
            }));
        }

        /// <summary>
        /// 参加中のロビーから出る。オーナーならロビーごと閉じる (Host がいなくなると試合が続けられないため)。
        /// 作成・参加の途中なら、終わったところで出る。入っていなければ何もせず成功にする。
        /// </summary>
        /// <param name="done">終わったときに呼ぶ (成功なら null、失敗なら理由)。null でもよい。</param>
        public void LeaveLobby(Action<string> done = null)
        {
            if (_disposed)
            {
                done?.Invoke(null);
                return;
            }

            if (_operation == Operation.Create || _operation == Operation.Join)
            {
                _leaveWhenSettled = true;
                done?.Invoke(null);
                return;
            }

            if (Current == null || _operation == Operation.Leave)
            {
                done?.Invoke(null);
                return;
            }

            if (_operation != Operation.None)
            {
                // ログイン中・検索中はロビーに入っていないので、ここには来ない
                throw new InvalidOperationException("cannot leave while " + _operation + " is in progress");
            }

            var lobby = Current;
            var destroy = lobby.IsOwnedBy(LocalUserId);
            Current = null;
            Begin(Operation.Leave);
            _backend.LeaveLobby(lobby.Id, destroy, Once((string error) =>
            {
                End();
                if (error != null)
                {
                    Debug.LogWarning(LogPrefix + "could not leave id=" + lobby.Id + ": " + error);
                }
                else
                {
                    Debug.Log(LogPrefix + (destroy ? "closed id=" : "left id=") + lobby.Id);
                }

                done?.Invoke(error);
            }));
        }

        /// <summary>ロビーに入っていれば出る要求を出し、バックエンドを閉じる。以後のコールバックは呼ばない。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // 作成・参加の途中で閉じると、終わったロビーに残ってしまう。出る要求だけは出しておく
            if (_operation == Operation.None && Current != null)
            {
                LeaveLobby();
            }

            _disposed = true;
            _backend.MembersChanged -= HandleMembersChanged;
            _backend.Removed -= HandleRemoved;
            _backend.LoggedOut -= HandleLoggedOut;
            _backend.Dispose();
            Current = null;
            MembersChanged = null;
            Removed = null;
            LoggedOut = null;
        }

        private void HandleMembersChanged(LobbyInfo lobby)
        {
            if (_disposed || lobby == null || Current == null || !string.Equals(lobby.Id, Current.Id, StringComparison.Ordinal))
            {
                return;
            }

            Current = lobby;
            LogMembers(lobby);
            MembersChanged?.Invoke(lobby);
        }

        private void HandleRemoved(string lobbyId, string reason)
        {
            if (_disposed || Current == null || !string.Equals(lobbyId, Current.Id, StringComparison.Ordinal))
            {
                return;
            }

            Current = null;
            Debug.LogWarning(LogPrefix + "removed from id=" + lobbyId + ": " + reason);
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

            Debug.LogWarning(LogPrefix + "logged out user=" + LocalUserId + ": " + reason);
            LocalUserId = null;
            LoggedOut?.Invoke(reason);
        }

        /// <summary>属性の一覧を検証して写す。null の要素やキーの重複は誤った呼び出しとして例外にする。</summary>
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

        /// <summary>作成・参加の結果を受け取る。成功なら <see cref="Current"/> にして true。</summary>
        private bool Enter(LobbyInfo lobby, ref string error)
        {
            if (error == null && lobby == null)
            {
                error = "the lobby service returned no lobby";
            }

            if (error == null && !lobby.HasMember(LocalUserId))
            {
                // メンバーの一覧が遅れて届くことがあるので、自分が載っていなくても入ったものとして扱う
                Debug.Log(LogPrefix + "id=" + lobby.Id + " does not list this user yet");
            }

            if (error != null)
            {
                _leaveWhenSettled = false;
                return false;
            }

            Current = lobby;
            return true;
        }

        /// <summary>作成・参加の途中で出るよう頼まれていれば出る。出たら true。</summary>
        private bool LeaveIfRequested()
        {
            if (!_leaveWhenSettled)
            {
                return false;
            }

            _leaveWhenSettled = false;
            LeaveLobby();
            return true;
        }

        private static void LogMembers(LobbyInfo lobby)
        {
            Debug.Log(LogPrefix + "members=" + lobby.DescribeMembers() + " id=" + lobby.Id);
        }

        private void Begin(Operation operation)
        {
            _operation = operation;
        }

        private void End()
        {
            _operation = Operation.None;
        }

        private void ThrowIfUnusable()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LobbyService));
            }

            if (_operation != Operation.None)
            {
                throw new InvalidOperationException(_operation + " is still in progress");
            }
        }

        private void ThrowUnlessReadyOutsideLobby()
        {
            ThrowIfUnusable();
            if (!IsLoggedIn)
            {
                throw new InvalidOperationException("log in before using lobbies");
            }

            if (Current != null)
            {
                throw new InvalidOperationException("already in lobby " + Current.Id + "; leave it first");
            }
        }

        /// <summary>Dispose 後と 2 回目以降の完了を捨てるコールバックにする。</summary>
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

        private static void Require(Delegate done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }
        }
    }
}
