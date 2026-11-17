using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    public sealed class MemberLobbies : IDisposable
    {
        private const string _interrupted = "the lobby was left or the login ended before the request finished";

        private readonly IMemberLobbyApi _api;
        private readonly Action<string> _log;
        private readonly HashSet<string> _joined = new HashSet<string>(StringComparer.Ordinal);
        private int _epoch;
        private bool _disposed;

        public MemberLobbies(IMemberLobbyApi api, Action<string> log)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _api.MemberUpdateReceived += HandleMemberUpdate;
        }

        public event Action<string, string> MemberAttributesChanged;

        public IReadOnlyCollection<string> Joined => _joined;

        public void Create(string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            if (string.IsNullOrEmpty(bucketId))
            {
                throw new ArgumentException("a bucket id is required", nameof(bucketId));
            }

            if (maxMembers < 1 || maxMembers > LobbyLaunchOptions.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxMembers), maxMembers, "must be between 1 and " + LobbyLaunchOptions.MaxMaxPlayers);
            }

            var copied = CopyAttributes(attributes, nameof(attributes));
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            var epoch = _epoch;
            _api.CreateLobby(bucketId, maxMembers, (lobbyId, error) =>
            {
                if (error == null && string.IsNullOrEmpty(lobbyId))
                {
                    error = "the lobby service created a lobby without an id";
                }

                if (_disposed)
                {
                    // 捨てられた後に作れたロビーは、知らせる相手がいないので出ておく
                    if (error == null)
                    {
                        LeaveQuietly(lobbyId);
                    }

                    return;
                }

                if (epoch != _epoch)
                {
                    done(null, _interrupted);
                    return;
                }

                if (error != null)
                {
                    _log(MemberLobbyLog.CreateFailed(bucketId, error));
                    done(null, error);
                    return;
                }

                _joined.Add(lobbyId);
                if (copied.Length == 0)
                {
                    FinishCreate(lobbyId, bucketId, done);
                    return;
                }

                _api.UpdateLobbyAttributes(lobbyId, copied, attributeError =>
                {
                    if (!Current(epoch, lobbyId, done))
                    {
                        return;
                    }

                    if (attributeError != null)
                    {
                        // 呼び出し側が属性で見分けるロビーなので、属性の無いまま残さない
                        Abandon(lobbyId, attributeError);
                        done(null, attributeError);
                        return;
                    }

                    FinishCreate(lobbyId, bucketId, done);
                });
            });
        }

        public void Join(string code, Action<LobbyInfo, string> done)
        {
            var lobbyId = code?.Trim();
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby code is required", nameof(code));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            if (_joined.Contains(lobbyId))
            {
                // サービスに入り直すと「もう入っている」で失敗するので、今の中身を返す
                var current = _api.CopyLobby(lobbyId, out var copyError);
                done(current, current == null ? copyError ?? "the lobby could not be read" : null);
                return;
            }

            var epoch = _epoch;
            _api.JoinLobby(lobbyId, error =>
            {
                if (_disposed)
                {
                    if (error == null)
                    {
                        LeaveQuietly(lobbyId);
                    }

                    return;
                }

                if (epoch != _epoch)
                {
                    done(null, _interrupted);
                    return;
                }

                if (error != null)
                {
                    _log(MemberLobbyLog.JoinFailed(lobbyId, error));
                    done(null, error);
                    return;
                }

                _joined.Add(lobbyId);
                var lobby = Read(lobbyId, out var readError);
                if (lobby == null)
                {
                    done(null, readError);
                    return;
                }

                _log(MemberLobbyLog.Joined(lobbyId, lobby.MemberCount, lobby.MaxMembers));
                done(lobby, null);
            });
        }

        public void Leave(string lobbyId, Action<string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            _api.LeaveLobby(lobbyId, error =>
            {
                if (_disposed)
                {
                    return;
                }

                // もう入っていないなら、出たのと同じ
                if (error != null && !LobbyBackendErrors.IsNotFound(error))
                {
                    _log(MemberLobbyLog.LeaveFailed(lobbyId, error));
                    done(error);
                    return;
                }

                _joined.Remove(lobbyId);
                _log(MemberLobbyLog.Left(lobbyId));
                done(null);
            });
        }

        public void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            var copied = CopyAttributes(attributes, nameof(attributes));
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            if (copied.Length == 0)
            {
                done(null);
                return;
            }

            _api.UpdateMemberAttributes(lobbyId, copied, error =>
            {
                if (!_disposed)
                {
                    done(error);
                }
            });
        }

        public void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("a user id is required", nameof(userId));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            var attributes = _api.CopyMemberAttributes(lobbyId, userId, out var reason);
            if (reason != null)
            {
                done(null, reason);
                return;
            }

            done(attributes ?? Array.Empty<LobbyAttribute>(), null);
        }

        public void Read(string lobbyId, Action<LobbyInfo, string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            var lobby = _api.CopyLobby(lobbyId, out var reason);
            if (lobby == null && (reason == null || LobbyBackendErrors.IsNotFound(reason)))
            {
                // 入っていない・無いロビーは読めないのではなく「無い」なので、理由を付けずに (null, null) で返す
                _joined.Remove(lobbyId);
                done(null, null);
                return;
            }

            if (lobby == null)
            {
                done(null, reason);
                return;
            }

            done(lobby, null);
        }

        // ログインが切れるとサービスの側でもうどのロビーにも入っていないので、出る要求を送らずに忘れる。
        public void Forget()
        {
            _epoch++;
            _joined.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var lobbyId in new List<string>(_joined))
            {
                LeaveQuietly(lobbyId);
            }

            _joined.Clear();
            _api.MemberUpdateReceived -= HandleMemberUpdate;
            MemberAttributesChanged = null;
        }

        private void FinishCreate(string lobbyId, string bucketId, Action<LobbyInfo, string> done)
        {
            var lobby = Read(lobbyId, out var error);
            if (lobby == null)
            {
                done(null, error);
                return;
            }

            _log(MemberLobbyLog.Created(lobbyId, bucketId, lobby.MemberCount, lobby.MaxMembers));
            done(lobby, null);
        }

        // 読めなければ、呼び出し側に ID を返せないまま入ったロビーが残らないよう出ておく。
        private LobbyInfo Read(string lobbyId, out string error)
        {
            var lobby = _api.CopyLobby(lobbyId, out error);
            if (lobby != null)
            {
                error = null;
                return lobby;
            }

            error = error ?? "the lobby could not be read";
            Abandon(lobbyId, error);
            return null;
        }

        private bool Current(int epoch, string lobbyId, Action<LobbyInfo, string> done)
        {
            if (_disposed)
            {
                return false;
            }

            if (epoch != _epoch || !_joined.Contains(lobbyId))
            {
                done(null, _interrupted);
                return false;
            }

            return true;
        }

        private void Abandon(string lobbyId, string reason)
        {
            _log(MemberLobbyLog.Abandoned(lobbyId, reason));
            _joined.Remove(lobbyId);
            LeaveQuietly(lobbyId);
        }

        private void LeaveQuietly(string lobbyId)
        {
            _api.LeaveLobby(lobbyId, error =>
            {
                if (error != null && !LobbyBackendErrors.IsNotFound(error))
                {
                    _log(MemberLobbyLog.LeaveFailed(lobbyId, error));
                }
            });
        }

        private void HandleMemberUpdate(string lobbyId, string userId)
        {
            if (_disposed || string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(userId) || !_joined.Contains(lobbyId))
            {
                return;
            }

            MemberAttributesChanged?.Invoke(lobbyId, userId);
        }

        // 呼び出し側が後から一覧を変えても、送る属性が変わらないよう写す。
        private static LobbyAttribute[] CopyAttributes(IReadOnlyList<LobbyAttribute> attributes, string name)
        {
            if (attributes == null)
            {
                throw new ArgumentNullException(name);
            }

            var copied = new LobbyAttribute[attributes.Count];
            for (var i = 0; i < copied.Length; i++)
            {
                copied[i] = attributes[i] ?? throw new ArgumentException("attribute " + i + " is null", name);
            }

            return copied;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MemberLobbies));
            }
        }
    }
}
