using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Platform;
using Fps.Core;
using Fps.Net;
using UnityEngine;
// Lobby と Connect の名前空間を両方開くと型名がぶつかりうるので、Connect は別名で参照する
using EosConnect = Epic.OnlineServices.Connect;
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
    /// 属性は作成の直後に公開の属性として付け、付けられなければロビーを閉じて失敗にする。
    /// Connect の認証の期限が近づいたら Device ID でログインし直し、ログインが切れたら <see cref="LoggedOut"/> で知らせる。
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
        private EosConnect.ConnectInterface _connect;
        private ProductUserId _localUserId;
        private EosDeviceLogin _login;
        private EosDeviceLogin _refresh;
        private ulong _memberStatusNotification;
        private ulong _authExpirationNotification;
        private ulong _loginStatusNotification;
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

        public event Action<string> LoggedOut;

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
            if (_login != null || _localUserId != null)
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
            _connect = connect;

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

        public void CreateLobby(
            string bucketId, int maxMembers, IReadOnlyList<Fps.Core.LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
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

                if (attributes.Count == 0 || _disposed)
                {
                    EnteredLobby(lobbyId, true, done);
                    return;
                }

                // CreateLobby は属性を受け取らないので、作った直後に足す
                ApplyAttributes(lobbyId, attributes, attributeError =>
                {
                    if (attributeError == null)
                    {
                        EnteredLobby(lobbyId, true, done);
                        return;
                    }

                    // 属性の無いサーバーのロビーは検索で選ばれても使えないので残さない
                    if (PlatformAlive)
                    {
                        SendLeave(lobbyId, true, _ => { });
                    }

                    Complete(done, null, attributeError);
                });
            });
        }

        public void UpdateAttributes(string lobbyId, IReadOnlyList<Fps.Core.LobbyAttribute> attributes, Action<string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            if (!CanCall(out var unavailable))
            {
                done(unavailable);
                return;
            }

            ApplyAttributes(lobbyId, attributes, error =>
            {
                if (!_disposed)
                {
                    done(error);
                }
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
            _refresh?.Cancel();
            _refresh = null;

            if (PlatformAlive)
            {
                if (_memberStatusNotification != 0)
                {
                    _lobby.RemoveNotifyLobbyMemberStatusReceived(_memberStatusNotification);
                }

                if (_authExpirationNotification != 0)
                {
                    _connect.RemoveNotifyAuthExpiration(_authExpirationNotification);
                }

                if (_loginStatusNotification != 0)
                {
                    _connect.RemoveNotifyLoginStatusChanged(_loginStatusNotification);
                }
            }

            _memberStatusNotification = 0;
            _authExpirationNotification = 0;
            _loginStatusNotification = 0;
            MembersChanged = null;
            Removed = null;
            LoggedOut = null;
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

            // ログインし直したときは登録済みの通知をそのまま使う
            if (_memberStatusNotification == 0)
            {
                var options = new AddNotifyLobbyMemberStatusReceivedOptions();
                _memberStatusNotification = _lobby.AddNotifyLobbyMemberStatusReceived(ref options, null, HandleMemberStatus);
                if (_memberStatusNotification == 0)
                {
                    done(null, "EOS lobby member notifications could not be registered");
                    return;
                }
            }

            if (_authExpirationNotification == 0)
            {
                var options = new EosConnect.AddNotifyAuthExpirationOptions();
                _authExpirationNotification = _connect.AddNotifyAuthExpiration(ref options, null, HandleAuthExpiration);
            }

            if (_loginStatusNotification == 0)
            {
                var options = new EosConnect.AddNotifyLoginStatusChangedOptions();
                _loginStatusNotification = _connect.AddNotifyLoginStatusChanged(ref options, null, HandleLoginStatusChanged);
            }

            if (_authExpirationNotification == 0 || _loginStatusNotification == 0)
            {
                // ログインはできているので続ける。長く動かすと認証の期限が切れ、ロビーから外れたときに気づけない
                Debug.LogWarning("[Lobby] EOS connect notifications could not be registered; the login will not be refreshed");
            }

            _localUserId = userId;
            done(userId.ToString(), null);
        }

        /// <summary>Connect の認証の期限が近い。期限が切れる前に Device ID でログインし直して更新する。</summary>
        private void HandleAuthExpiration(ref EosConnect.AuthExpirationCallbackInfo info)
        {
            if (_disposed || _refresh != null || _localUserId == null || !IsLocalUser(info.LocalUserId) || !PlatformAlive)
            {
                return;
            }

            _refresh = new EosDeviceLogin(_connect, _displayName, SystemInfo.deviceModel);
            _refresh.Begin((userId, error) =>
            {
                _refresh = null;
                if (error != null)
                {
                    // 期限が切れればログインの状態の通知が来て、LoggedOut からやり直しになる
                    Debug.LogWarning("[Lobby] refreshing the EOS login failed: " + error);
                    return;
                }

                Debug.Log("[Lobby] refreshed the EOS login user=" + userId);
            });
        }

        private void HandleLoginStatusChanged(ref EosConnect.LoginStatusChangedCallbackInfo info)
        {
            if (_disposed || _localUserId == null || !IsLocalUser(info.LocalUserId) || info.CurrentStatus == LoginStatus.LoggedIn)
            {
                return;
            }

            var reason = "the EOS connect login became " + info.CurrentStatus;
            EosPlatform.ForgetLogin(_localUserId);
            _localUserId = null;
            _refresh?.Cancel();
            _refresh = null;

            var lobbyId = _currentLobbyId;
            _currentLobbyId = null;
            if (lobbyId != null)
            {
                Removed?.Invoke(lobbyId, reason);
            }

            LoggedOut?.Invoke(reason);
        }

        private bool IsLocalUser(ProductUserId userId)
        {
            return userId != null && _localUserId != null
                && string.Equals(userId.ToString(), _localUserId.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// オーナーとして入っているロビーに公開の属性を足して送る。結果は (Dispose 後でも) 1 回だけ渡すので、
        /// 呼び出し側が Dispose 後の扱いを決める。
        /// </summary>
        private void ApplyAttributes(string lobbyId, IReadOnlyList<Fps.Core.LobbyAttribute> attributes, Action<string> done)
        {
            var modificationOptions = new UpdateLobbyModificationOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            var result = _lobby.UpdateLobbyModification(ref modificationOptions, out var modification);
            if (result != Result.Success || modification == null)
            {
                done("preparing the update of EOS lobby " + lobbyId + " failed: " + result);
                return;
            }

            try
            {
                foreach (var attribute in attributes)
                {
                    var addOptions = new LobbyModificationAddAttributeOptions
                    {
                        Attribute = new AttributeData
                        {
                            Key = attribute.Key,
                            Value = attribute.IsText
                                ? new AttributeDataValue { AsUtf8 = attribute.Text }
                                : new AttributeDataValue { AsInt64 = attribute.Number },
                        },
                        Visibility = LobbyAttributeVisibility.Public,
                    };
                    result = modification.AddAttribute(ref addOptions);
                    if (result != Result.Success)
                    {
                        done("setting " + attribute + " on EOS lobby " + lobbyId + " failed: " + result);
                        return;
                    }
                }

                var updateOptions = new UpdateLobbyOptions { LobbyModificationHandle = modification };
                _lobby.UpdateLobby(ref updateOptions, null, (ref UpdateLobbyCallbackInfo info) =>
                    done(info.ResultCode == Result.Success
                        ? null
                        : "updating EOS lobby " + lobbyId + " failed: " + info.ResultCode));
            }
            finally
            {
                // UpdateLobby は呼んだ時点で変更を写し取るので、完了を待たずに解放してよい
                modification.Release();
            }
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
            return new LobbyInfo(
                lobbyId, owner.ToString(), (int)Math.Min(info.MaxMembers, (uint)int.MaxValue), members, ReadAttributes(details, lobbyId));
        }

        /// <summary>
        /// ロビーの公開の属性のうち、文字列と整数のものを読む。読めない・形の合わない属性は飛ばす
        /// (クイックマッチの選び方が、欠けた属性のロビーを選ばないので、ロビーごと捨てなくてよい)。
        /// </summary>
        private static List<Fps.Core.LobbyAttribute> ReadAttributes(LobbyDetails details, string lobbyId)
        {
            var countOptions = new LobbyDetailsGetAttributeCountOptions();
            var count = details.GetAttributeCount(ref countOptions);
            var attributes = new List<Fps.Core.LobbyAttribute>((int)Math.Min(count, 64u));
            for (uint i = 0; i < count; i++)
            {
                var copyOptions = new LobbyDetailsCopyAttributeByIndexOptions { AttrIndex = i };
                var result = details.CopyAttributeByIndex(ref copyOptions, out var copied);
                if (result != Result.Success || !copied.HasValue || !copied.Value.Data.HasValue)
                {
                    Debug.LogWarning("[Lobby] skipped attribute " + i + " of id=" + lobbyId + ": " + result);
                    continue;
                }

                var data = copied.Value.Data.Value;
                var key = Text(data.Key);
                if (key == null)
                {
                    continue;
                }

                // EOS は属性キーの大文字・小文字を区別せず、大文字にして返すことがある。作る側 (Fps.Core.LobbyAttribute) の表記に戻す
                key = key.ToLowerInvariant();
                try
                {
                    switch (data.Value.ValueType)
                    {
                        case AttributeType.String:
                            var text = Text(data.Value.AsUtf8);
                            if (text != null)
                            {
                                attributes.Add(Fps.Core.LobbyAttribute.OfText(key, text));
                            }

                            break;
                        case AttributeType.Int64:
                            if (data.Value.AsInt64.HasValue)
                            {
                                attributes.Add(Fps.Core.LobbyAttribute.OfNumber(key, data.Value.AsInt64.Value));
                            }

                            break;
                    }
                }
                catch (ArgumentException e)
                {
                    // 同じバケットに別の作り方のロビーが混ざっても、検索全体は失敗させない
                    Debug.LogWarning("[Lobby] skipped attribute " + key + " of id=" + lobbyId + ": " + e.Message);
                }
            }

            return attributes;
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
