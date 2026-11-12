using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Platform;
using Fps.Core;
using Fps.Net;
using UnityEngine;
// SDK の名前空間に同名の型が増えても、こちらを指すようにする
using LobbyInfo = Fps.Core.LobbyInfo;

namespace Fps.NetEos
{
    /// <summary>
    /// EOS の Connect (Device ID) と Lobby インターフェースで <see cref="ILobbyBackend"/> を実装する。
    /// SDK の型はここから外に出さず、ID は文字列、ロビーは <see cref="LobbyInfo"/> にして返す。
    /// </summary>
    /// <remarks>
    /// ロビーは公開 (検索で見つかる)・プレゼンス無し・ID での参加を許可・ホスト移行なしで作る。
    /// Host が抜けたら試合は続けられないので、オーナーを他のメンバーに移さない。
    /// プラットフォームが解放された後 (終了時) は SDK を呼ばず、失敗として返す。
    /// </remarks>
    internal sealed class EosLobbyBackend : ILobbyBackend
    {
        /// <summary>バケット ID で検索するときの属性名 (EOS_LOBBY_SEARCH_BUCKET_ID)。</summary>
        private const string _searchBucketKey = "bucket";

        private readonly EosCredentials _credentials;
        private readonly string _displayName;
        private PlatformInterface _platform;
        private LobbyInterface _lobby;
        private ProductUserId _localUserId;
        private EosDeviceLogin _login;
        private ulong _memberStatusNotification;
        private string _currentLobbyId;
        private bool _disposed;

        /// <param name="credentials">プラットフォームを作るのに使う認証情報。</param>
        /// <param name="displayName">初めてログインしたときに作るユーザーの表示名。</param>
        public EosLobbyBackend(EosCredentials credentials, string displayName)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _displayName = string.IsNullOrWhiteSpace(displayName) ? "fps-player" : displayName;
        }

        public event Action<LobbyInfo> MembersChanged;

        public event Action<string, string> Removed;

        /// <summary>このバックエンドが使っているプラットフォームが、まだ解放されていないか。</summary>
        private bool PlatformAlive => _platform != null && ReferenceEquals(EosPlatform.Current, _platform);

        public void Tick()
        {
            if (!_disposed && PlatformAlive)
            {
                EosPlatform.Tick();
            }
        }

        public void Login(Action<string, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            if (_platform != null)
            {
                throw new InvalidOperationException("already logged in or logging in");
            }

            // SDK の関数はプラットフォームの初期化後に呼ぶ
            var platform = EosPlatform.Acquire(_credentials, out var error);
            if (platform == null)
            {
                done(null, error);
                return;
            }

            var lobby = platform.GetLobbyInterface();
            var connect = platform.GetConnectInterface();
            if (lobby == null || connect == null)
            {
                done(null, "EOS Lobby or Connect interface is unavailable");
                return;
            }

            _platform = platform;
            _lobby = lobby;

            var loggedIn = EosPlatform.LocalUserId;
            if (loggedIn != null)
            {
                FinishLogin(loggedIn, null, done);
                return;
            }

            _login = new EosDeviceLogin(connect, _displayName, SystemInfo.deviceModel);
            _login.Begin((userId, loginError) =>
            {
                _login = null;
                FinishLogin(userId, loginError, done);
            });
        }

        public void CreateLobby(string bucketId, int maxMembers, Action<LobbyInfo, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!CanCall(out var unavailable))
            {
                done(null, unavailable);
                return;
            }

            var options = new CreateLobbyOptions
            {
                LocalUserId = _localUserId,
                MaxLobbyMembers = (uint)maxMembers,
                PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                PresenceEnabled = false,
                AllowInvites = false,
                BucketId = bucketId,
                DisableHostMigration = true,
                EnableRTCRoom = false,
                EnableJoinById = true,
                RejoinAfterKickRequiresInvite = false,
            };
            _lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
            {
                var lobbyId = Text(info.LobbyId);
                if (info.ResultCode != Result.Success || lobbyId == null)
                {
                    Complete(done, null, "creating the EOS lobby failed: " + info.ResultCode);
                    return;
                }

                EnteredLobby(lobbyId, true, done);
            });
        }

        public void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!CanCall(out var unavailable))
            {
                done(null, unavailable);
                return;
            }

            var createOptions = new CreateLobbySearchOptions { MaxResults = (uint)Math.Max(1, maxResults) };
            var result = _lobby.CreateLobbySearch(ref createOptions, out var search);
            if (result != Result.Success || search == null)
            {
                done(null, "creating the EOS lobby search failed: " + result);
                return;
            }

            var parameterOptions = new LobbySearchSetParameterOptions
            {
                Parameter = new AttributeData
                {
                    Key = _searchBucketKey,
                    Value = new AttributeDataValue { AsUtf8 = bucketId },
                },
                ComparisonOp = ComparisonOp.Equal,
            };
            result = search.SetParameter(ref parameterOptions);
            if (result != Result.Success)
            {
                search.Release();
                done(null, "setting the EOS lobby search bucket failed: " + result);
                return;
            }

            var findOptions = new LobbySearchFindOptions { LocalUserId = _localUserId };
            search.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo info) =>
            {
                try
                {
                    if (_disposed)
                    {
                        return;
                    }

                    // 該当なしは NotFound で返る
                    if (info.ResultCode == Result.NotFound)
                    {
                        done(Array.Empty<LobbyInfo>(), null);
                        return;
                    }

                    if (info.ResultCode != Result.Success)
                    {
                        done(null, "searching EOS lobbies failed: " + info.ResultCode);
                        return;
                    }

                    done(ReadResults(search), null);
                }
                finally
                {
                    search.Release();
                }
            });
        }

        public void JoinLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!CanCall(out var unavailable))
            {
                done(null, unavailable);
                return;
            }

            var options = new JoinLobbyByIdOptions
            {
                LobbyId = lobbyId,
                LocalUserId = _localUserId,
                PresenceEnabled = false,
            };
            _lobby.JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success)
                {
                    Complete(done, null, "joining EOS lobby " + lobbyId + " failed: " + info.ResultCode);
                    return;
                }

                EnteredLobby(Text(info.LobbyId) ?? lobbyId, false, done);
            });
        }

        public void LeaveLobby(string lobbyId, bool destroy, Action<string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (string.Equals(_currentLobbyId, lobbyId, StringComparison.Ordinal))
            {
                _currentLobbyId = null;
            }

            // 終了時にプラットフォームが先に解放されていれば、EOS 側が接続切れとしてロビーから外す
            if (!CanCall(out var unavailable))
            {
                done(unavailable);
                return;
            }

            SendLeave(lobbyId, destroy, done);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _login?.Cancel();
            _login = null;

            if (PlatformAlive && _memberStatusNotification != 0)
            {
                _lobby.RemoveNotifyLobbyMemberStatusReceived(_memberStatusNotification);
            }

            _memberStatusNotification = 0;
            MembersChanged = null;
            Removed = null;
        }

        private void FinishLogin(ProductUserId userId, string error, Action<string, string> done)
        {
            if (_disposed)
            {
                return;
            }

            if (error != null)
            {
                done(null, error);
                return;
            }

            if (!PlatformAlive)
            {
                done(null, "the EOS platform was released during login");
                return;
            }

            _localUserId = userId;
            var options = new AddNotifyLobbyMemberStatusReceivedOptions();
            _memberStatusNotification = _lobby.AddNotifyLobbyMemberStatusReceived(ref options, null, HandleMemberStatus);
            if (_memberStatusNotification == 0)
            {
                done(null, "EOS lobby member notifications could not be registered");
                return;
            }

            done(userId.ToString(), null);
        }

        /// <summary>作成・参加が終わったロビーを読んで返す。Dispose 後に終わったなら、残らないよう出る。</summary>
        private void EnteredLobby(string lobbyId, bool owner, Action<LobbyInfo, string> done)
        {
            if (_disposed)
            {
                if (PlatformAlive)
                {
                    SendLeave(lobbyId, owner, _ => { });
                }

                return;
            }

            var lobby = ReadLobby(lobbyId, out var error);
            if (lobby == null)
            {
                // 入ったのに中身が読めないロビーには留まらない
                SendLeave(lobbyId, owner, _ => { });
                done(null, error);
                return;
            }

            _currentLobbyId = lobbyId;
            done(lobby, null);
        }

        private void SendLeave(string lobbyId, bool destroy, Action<string> done)
        {
            if (destroy)
            {
                var options = new DestroyLobbyOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
                _lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo info) =>
                    CompleteLeave(done, "closing", lobbyId, info.ResultCode));
                return;
            }

            var leaveOptions = new LeaveLobbyOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            _lobby.LeaveLobby(ref leaveOptions, null, (ref LeaveLobbyCallbackInfo info) =>
                CompleteLeave(done, "leaving", lobbyId, info.ResultCode));
        }

        private void CompleteLeave(Action<string> done, string action, string lobbyId, Result result)
        {
            if (_disposed)
            {
                return;
            }

            // 既に無いロビーから出るのは成功とみなす
            done(result == Result.Success || result == Result.NotFound
                ? null
                : action + " EOS lobby " + lobbyId + " failed: " + result);
        }

        private void HandleMemberStatus(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            var lobbyId = Text(info.LobbyId);
            if (_disposed || _currentLobbyId == null || !string.Equals(lobbyId, _currentLobbyId, StringComparison.Ordinal))
            {
                return;
            }

            var isSelf = info.TargetUserId != null && _localUserId != null
                && string.Equals(info.TargetUserId.ToString(), _localUserId.ToString(), StringComparison.OrdinalIgnoreCase);
            var status = info.CurrentStatus;
            if (status == LobbyMemberStatus.Closed
                || (isSelf && (status == LobbyMemberStatus.Kicked
                    || status == LobbyMemberStatus.Left
                    || status == LobbyMemberStatus.Disconnected)))
            {
                _currentLobbyId = null;
                Removed?.Invoke(lobbyId, status == LobbyMemberStatus.Closed ? "the lobby was closed" : "this user was " + status);
                return;
            }

            var lobby = ReadLobby(lobbyId, out var error);
            if (lobby == null)
            {
                Debug.LogWarning("[Lobby] could not read id=" + lobbyId + " after a member " + status + ": " + error);
                return;
            }

            MembersChanged?.Invoke(lobby);
        }

        /// <summary>参加中のロビーの今の状態を読む。読めなければ null と理由。</summary>
        private LobbyInfo ReadLobby(string lobbyId, out string error)
        {
            if (!PlatformAlive)
            {
                error = "the EOS platform was released";
                return null;
            }

            var options = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            var result = _lobby.CopyLobbyDetailsHandle(ref options, out var details);
            if (result != Result.Success || details == null)
            {
                error = "reading EOS lobby " + lobbyId + " failed: " + result;
                return null;
            }

            try
            {
                return Describe(details, out error);
            }
            finally
            {
                details.Release();
            }
        }

        private static List<LobbyInfo> ReadResults(LobbySearch search)
        {
            var countOptions = new LobbySearchGetSearchResultCountOptions();
            var count = search.GetSearchResultCount(ref countOptions);
            var lobbies = new List<LobbyInfo>((int)Math.Min(count, 256u));
            for (uint i = 0; i < count; i++)
            {
                var copyOptions = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                var result = search.CopySearchResultByIndex(ref copyOptions, out var details);
                if (result != Result.Success || details == null)
                {
                    Debug.LogWarning("[Lobby] skipped search result " + i + ": " + result);
                    continue;
                }

                try
                {
                    var lobby = Describe(details, out var error);
                    if (lobby == null)
                    {
                        Debug.LogWarning("[Lobby] skipped search result " + i + ": " + error);
                        continue;
                    }

                    lobbies.Add(lobby);
                }
                finally
                {
                    details.Release();
                }
            }

            return lobbies;
        }

        /// <summary>LobbyDetails を <see cref="LobbyInfo"/> にする。ハンドルの解放は呼び出し側で行う。</summary>
        private static LobbyInfo Describe(LobbyDetails details, out string error)
        {
            var infoOptions = new LobbyDetailsCopyInfoOptions();
            var result = details.CopyInfo(ref infoOptions, out var copied);
            if (result != Result.Success || !copied.HasValue)
            {
                error = "reading EOS lobby details failed: " + result;
                return null;
            }

            var info = copied.Value;
            var lobbyId = Text(info.LobbyId);
            var owner = info.LobbyOwnerUserId;
            if (lobbyId == null || owner == null || !owner.IsValid() || info.MaxMembers == 0)
            {
                error = "EOS lobby details are incomplete (id=" + lobbyId + ")";
                return null;
            }

            var countOptions = new LobbyDetailsGetMemberCountOptions();
            var count = details.GetMemberCount(ref countOptions);
            var members = new List<string>((int)Math.Min(count, (uint)LobbyLaunchOptions.MaxMaxPlayers));
            for (uint i = 0; i < count; i++)
            {
                var memberOptions = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                var member = details.GetMemberByIndex(ref memberOptions);
                if (member != null && member.IsValid())
                {
                    members.Add(member.ToString());
                }
            }

            error = null;
            return new LobbyInfo(lobbyId, owner.ToString(), (int)Math.Min(info.MaxMembers, (uint)int.MaxValue), members);
        }

        private bool CanCall(out string error)
        {
            ThrowIfDisposed();
            if (_localUserId == null)
            {
                error = "not logged in to EOS";
                return false;
            }

            if (!PlatformAlive)
            {
                error = "the EOS platform was released";
                return false;
            }

            error = null;
            return true;
        }

        private void Complete(Action<LobbyInfo, string> done, LobbyInfo lobby, string error)
        {
            if (!_disposed)
            {
                done(lobby, error);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EosLobbyBackend));
            }
        }

        /// <summary>SDK の文字列を string にする。null や空なら null。</summary>
        private static string Text(Utf8String value)
        {
            // Utf8String の演算子を通さずに null を判定する
            if ((object)value == null)
            {
                return null;
            }

            var text = value.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
