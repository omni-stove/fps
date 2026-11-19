using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.Platform;
using Shooter.Core;
using Shooter.NetV2;
using UnityEngine;
// Lobby と Connect の名前空間を両方開くと型名がぶつかりうるので、Connect は別名で参照する
using EosConnect = Epic.OnlineServices.Connect;
// SDK の名前空間に同名の型が増えても、こちらを指すようにする
using LobbyInfo = Shooter.Core.LobbyInfo;

namespace Shooter.NetEos
{
    internal sealed class EosLobbyBackend : ILobbyBackend
    {
        // EOS がバケット ID の検索に決めている属性名 (EOS_LOBBY_SEARCH_BUCKET_ID) なので変えられない。
        private const string _searchBucketKey = "bucket";

        private readonly EosCredentials _credentials;
        private readonly string _displayName;
        private readonly HashSet<string> _matchLobbies = new HashSet<string>(StringComparer.Ordinal);
        private readonly MemberLobbyApi _memberApi;
        private readonly MemberLobbies _members;
        private PlatformInterface _platform;
        private LobbyInterface _lobby;
        private EosConnect.ConnectInterface _connect;
        private ProductUserId _localUserId;
        private EosDeviceLogin _login;
        private IEosLogin _refresh;
        private ulong _authExpirationNotification;
        private ulong _loginStatusNotification;
        private ulong _memberUpdateNotification;
        private bool _disposed;

        public EosLobbyBackend(EosCredentials credentials, string displayName)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _displayName = string.IsNullOrWhiteSpace(displayName) ? "shooter-player" : displayName;
            _memberApi = new MemberLobbyApi(this);
            _members = new MemberLobbies(_memberApi, message => Debug.Log(message));
        }

        public event Action<string> LoggedOut;

        public event Action<string, string> MemberAttributesChanged
        {
            add => _members.MemberAttributesChanged += value;
            remove => _members.MemberAttributesChanged -= value;
        }

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

        public void CreateMemberLobby(
            string bucketId, int maxMembers, IReadOnlyList<Shooter.Core.LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            _members.Create(bucketId, maxMembers, attributes, done);
        }

        public void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            _members.Join(lobbyId, done);
        }

        public void LeaveMemberLobby(string lobbyId, Action<string> done)
        {
            _members.Leave(lobbyId, done);
        }

        public void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            _members.Read(lobbyId, done);
        }

        public void SetMemberAttributes(string lobbyId, IReadOnlyList<Shooter.Core.LobbyAttribute> attributes, Action<string> done)
        {
            _members.SetMemberAttributes(lobbyId, attributes, done);
        }

        public void ReadMemberAttributes(
            string lobbyId, string userId, Action<IReadOnlyList<Shooter.Core.LobbyAttribute>, string> done)
        {
            _members.ReadMemberAttributes(lobbyId, userId, done);
        }

        public void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

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
                // ボイスを使わない人も入るので、RTC 部屋には EosVoiceSession が入るときにだけ入る
                RTCRoomJoinActionType = LobbyRTCRoomJoinActionType.ManualJoin,
                LocalRTCOptions = EosVoiceSession.RoomOptions(),
            };
            _lobby.JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo info) =>
            {
                if (_disposed)
                {
                    // 返事を待つ間に捨てられると Dispose の出る対象に入っていないので、入れた分はここで出る
                    if (info.ResultCode == Result.Success && PlatformAlive && _localUserId != null)
                    {
                        LeaveQuietly(lobbyId);
                    }

                    return;
                }

                if (info.ResultCode != Result.Success)
                {
                    done(null, "joining the EOS match lobby id=" + lobbyId + " failed: " + info.ResultCode);
                    return;
                }

                _matchLobbies.Add(lobbyId);
                var lobby = CopyLobby(lobbyId, out var error);
                if (lobby == null)
                {
                    // 入ったことを返せないまま残ると、呼び出し側が出ないので、ここで出ておく
                    LeaveQuietly(lobbyId);
                    done(null, error);
                    return;
                }

                done(lobby, null);
            });
        }

        public void LeaveMatchLobby(string lobbyId, Action<string> done)
        {
            if (string.IsNullOrEmpty(lobbyId))
            {
                throw new ArgumentException("a lobby id is required", nameof(lobbyId));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!CanCall(out var unavailable))
            {
                done(unavailable);
                return;
            }

            var options = new LeaveLobbyOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            _lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
            {
                if (_disposed)
                {
                    return;
                }

                // もう入っていない (サーバーがロビーを閉じた・追い出された) なら、出たのと同じ
                if (info.ResultCode != Result.Success && info.ResultCode != Result.NotFound)
                {
                    done("leaving the EOS match lobby id=" + lobbyId + " failed: " + info.ResultCode);
                    return;
                }

                _matchLobbies.Remove(lobbyId);
                done(null);
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (PlatformAlive && _localUserId != null)
            {
                // 出ずに捨てると、EOS は接続が切れたと気づくまでメンバーとして残し、ボイスの部屋にも名前が残る
                foreach (var lobbyId in new List<string>(_matchLobbies))
                {
                    LeaveQuietly(lobbyId);
                }
            }
            else
            {
                // 出る要求を送れないので、送れなかったログを出させずに忘れる
                _members.Forget();
            }

            _members.Dispose();
            _matchLobbies.Clear();
            _login?.Cancel();
            _login = null;
            _refresh?.Cancel();
            _refresh = null;

            if (PlatformAlive)
            {
                if (_authExpirationNotification != 0)
                {
                    _connect.RemoveNotifyAuthExpiration(_authExpirationNotification);
                }

                if (_loginStatusNotification != 0)
                {
                    _connect.RemoveNotifyLoginStatusChanged(_loginStatusNotification);
                }

                if (_memberUpdateNotification != 0)
                {
                    _lobby.RemoveNotifyLobbyMemberUpdateReceived(_memberUpdateNotification);
                }
            }

            _authExpirationNotification = 0;
            _loginStatusNotification = 0;
            _memberUpdateNotification = 0;
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
                // ログインはできているので続ける。探している間に認証の期限が切れても気づけない
                Debug.LogWarning("[Lobby] EOS connect notifications could not be registered; the login will not be refreshed");
            }

            if (_memberUpdateNotification == 0)
            {
                var options = new AddNotifyLobbyMemberUpdateReceivedOptions();
                _memberUpdateNotification = _lobby.AddNotifyLobbyMemberUpdateReceived(ref options, null, HandleMemberUpdate);
                if (_memberUpdateNotification == 0)
                {
                    // クイックマッチには要らないのでログインは失敗にしない。パーティは属性を読み直すまで変化に気づけない
                    Debug.LogWarning("[Lobby] EOS lobby member update notifications could not be registered");
                }
            }

            _localUserId = userId;
            done(userId.ToString(), null);
        }

        private void HandleAuthExpiration(ref EosConnect.AuthExpirationCallbackInfo info)
        {
            if (_disposed || _refresh != null || _localUserId == null || !IsLocalUser(info.LocalUserId) || !PlatformAlive)
            {
                return;
            }

            // Epic アカウントで入ったユーザーを Device ID で入り直すと別のユーザーになるので、入れた手段で入り直す。
            _refresh = EosPlatform.CreateRefresh(_connect, _localUserId, _displayName, SystemInfo.deviceModel);
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
            _matchLobbies.Clear();
            _members.Forget();
            _refresh?.Cancel();
            _refresh = null;
            LoggedOut?.Invoke(reason);
        }

        private bool IsLocalUser(ProductUserId userId)
        {
            return userId != null && _localUserId != null
                && string.Equals(userId.ToString(), _localUserId.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private LobbyInfo CopyLobby(string lobbyId, out string error)
        {
            var options = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            var result = _lobby.CopyLobbyDetailsHandle(ref options, out var details);
            if (result != Result.Success || details == null)
            {
                error = result == Result.NotFound
                    ? LobbyBackendErrors.NotFound
                    : "reading the EOS lobby id=" + lobbyId + " failed: " + result;
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

        // 結果を待つ相手がいないので、失敗はログにだけ出す。
        private void LeaveQuietly(string lobbyId)
        {
            _matchLobbies.Remove(lobbyId);
            var options = new LeaveLobbyOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            _lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success && info.ResultCode != Result.NotFound)
                {
                    Debug.LogWarning("[Lobby] leaving the EOS match lobby id=" + lobbyId + " failed: " + info.ResultCode);
                }
            });
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

        // ハンドルは呼び出し側が解放するので、ここでは解放しない。
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

        // 読めない・形の合わない属性は飛ばすだけでロビーごと捨てないのは、クイックマッチの選び方が欠けた属性のロビーを選ばないため。
        private static List<Shooter.Core.LobbyAttribute> ReadAttributes(LobbyDetails details, string lobbyId)
        {
            var countOptions = new LobbyDetailsGetAttributeCountOptions();
            var count = details.GetAttributeCount(ref countOptions);
            return ReadAttributes(count, (uint index, out Epic.OnlineServices.Lobby.Attribute? copied) =>
            {
                var copyOptions = new LobbyDetailsCopyAttributeByIndexOptions { AttrIndex = index };
                return details.CopyAttributeByIndex(ref copyOptions, out copied);
            }, lobbyId);
        }

        // 読めない・形の合わない属性はロビーの属性と同じく飛ばす。パーティは欠けた属性を「まだ書いていない」として扱うため。
        private static List<Shooter.Core.LobbyAttribute> ReadMemberAttributes(LobbyDetails details, ProductUserId member, string lobbyId)
        {
            var countOptions = new LobbyDetailsGetMemberAttributeCountOptions { TargetUserId = member };
            var count = details.GetMemberAttributeCount(ref countOptions);
            return ReadAttributes(count, (uint index, out Epic.OnlineServices.Lobby.Attribute? copied) =>
            {
                var copyOptions = new LobbyDetailsCopyMemberAttributeByIndexOptions { TargetUserId = member, AttrIndex = index };
                return details.CopyMemberAttributeByIndex(ref copyOptions, out copied);
            }, lobbyId);
        }

        private static List<Shooter.Core.LobbyAttribute> ReadAttributes(uint count, CopyAttributeAt copy, string lobbyId)
        {
            var attributes = new List<Shooter.Core.LobbyAttribute>((int)Math.Min(count, 64u));
            for (uint i = 0; i < count; i++)
            {
                var result = copy(i, out var copied);
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

                // EOS は属性キーの大文字・小文字を区別せず、大文字にして返すことがある。作る側 (Shooter.Core.LobbyAttribute) の表記に戻す
                key = key.ToLowerInvariant();
                try
                {
                    switch (data.Value.ValueType)
                    {
                        case AttributeType.String:
                            var text = Text(data.Value.AsUtf8);
                            if (text != null)
                            {
                                attributes.Add(Shooter.Core.LobbyAttribute.OfText(key, text));
                            }

                            break;
                        case AttributeType.Int64:
                            if (data.Value.AsInt64.HasValue)
                            {
                                attributes.Add(Shooter.Core.LobbyAttribute.OfNumber(key, data.Value.AsInt64.Value));
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

        private void HandleMemberUpdate(ref LobbyMemberUpdateReceivedCallbackInfo info)
        {
            if (_disposed)
            {
                return;
            }

            var lobbyId = Text(info.LobbyId);
            var member = info.TargetUserId;
            if (lobbyId == null || member == null || !member.IsValid())
            {
                return;
            }

            _memberApi.RaiseMemberUpdate(lobbyId, member.ToString());
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EosLobbyBackend));
            }
        }

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

        private delegate Result CopyAttributeAt(uint index, out Epic.OnlineServices.Lobby.Attribute? attribute);

        private sealed class MemberLobbyApi : IMemberLobbyApi
        {
            private readonly EosLobbyBackend _owner;

            public MemberLobbyApi(EosLobbyBackend owner)
            {
                _owner = owner;
            }

            public event Action<string, string> MemberUpdateReceived;

            public void RaiseMemberUpdate(string lobbyId, string userId)
            {
                MemberUpdateReceived?.Invoke(lobbyId, userId);
            }

            public void CreateLobby(string bucketId, int maxMembers, Action<string, string> done)
            {
                if (!Ready(out var unavailable))
                {
                    done(null, unavailable);
                    return;
                }

                var options = new CreateLobbyOptions
                {
                    LocalUserId = _owner._localUserId,
                    MaxLobbyMembers = (uint)maxMembers,
                    // 公開の検索に出さない。Device ID のユーザーにはフレンドもプレゼンスも無いので、ID で入るほかに入り口は無い
                    PermissionLevel = LobbyPermissionLevel.Joinviapresence,
                    PresenceEnabled = false,
                    AllowInvites = false,
                    BucketId = bucketId,
                    DisableHostMigration = false,
                    EnableRTCRoom = false,
                    EnableJoinById = true,
                    RejoinAfterKickRequiresInvite = false,
                };
                _owner._lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
                {
                    if (info.ResultCode != Result.Success)
                    {
                        done(null, "creating the EOS member lobby failed: " + info.ResultCode);
                        return;
                    }

                    done(Text(info.LobbyId), null);
                });
            }

            public void UpdateLobbyAttributes(string lobbyId, IReadOnlyList<Shooter.Core.LobbyAttribute> attributes, Action<string> done)
            {
                Modify(lobbyId, attributes, false, done);
            }

            public void UpdateMemberAttributes(string lobbyId, IReadOnlyList<Shooter.Core.LobbyAttribute> attributes, Action<string> done)
            {
                Modify(lobbyId, attributes, true, done);
            }

            public void JoinLobby(string lobbyId, Action<string> done)
            {
                if (!Ready(out var unavailable))
                {
                    done(unavailable);
                    return;
                }

                var options = new JoinLobbyByIdOptions
                {
                    LobbyId = lobbyId,
                    LocalUserId = _owner._localUserId,
                    PresenceEnabled = false,
                };
                _owner._lobby.JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo info) =>
                {
                    // 打ち間違えたコードは ID の形をしていないことがあり、無いロビーと同じに扱う
                    done(info.ResultCode == Result.Success
                        ? null
                        : info.ResultCode == Result.InvalidParameters
                            ? LobbyBackendErrors.NotFound
                            : Failure("joining", lobbyId, info.ResultCode));
                });
            }

            public void LeaveLobby(string lobbyId, Action<string> done)
            {
                if (!Ready(out var unavailable))
                {
                    done(unavailable);
                    return;
                }

                var options = new LeaveLobbyOptions { LobbyId = lobbyId, LocalUserId = _owner._localUserId };
                _owner._lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
                    done(info.ResultCode == Result.Success ? null : Failure("leaving", lobbyId, info.ResultCode)));
            }

            public LobbyInfo CopyLobby(string lobbyId, out string error)
            {
                return Ready(out error) ? _owner.CopyLobby(lobbyId, out error) : null;
            }

            public IReadOnlyList<Shooter.Core.LobbyAttribute> CopyMemberAttributes(string lobbyId, string userId, out string error)
            {
                if (!Ready(out error))
                {
                    return null;
                }

                var member = ProductUserId.FromString(userId);
                if (member == null || !member.IsValid())
                {
                    error = "not an EOS product user id: " + userId;
                    return null;
                }

                var options = new CopyLobbyDetailsHandleOptions { LobbyId = lobbyId, LocalUserId = _owner._localUserId };
                var result = _owner._lobby.CopyLobbyDetailsHandle(ref options, out var details);
                if (result != Result.Success || details == null)
                {
                    error = Failure("reading", lobbyId, result);
                    return null;
                }

                try
                {
                    error = null;
                    return ReadMemberAttributes(details, member, lobbyId);
                }
                finally
                {
                    details.Release();
                }
            }

            private void Modify(string lobbyId, IReadOnlyList<Shooter.Core.LobbyAttribute> attributes, bool member, Action<string> done)
            {
                if (!Ready(out var unavailable))
                {
                    done(unavailable);
                    return;
                }

                var modificationOptions = new UpdateLobbyModificationOptions { LobbyId = lobbyId, LocalUserId = _owner._localUserId };
                var result = _owner._lobby.UpdateLobbyModification(ref modificationOptions, out var modification);
                if (result != Result.Success || modification == null)
                {
                    done(Failure("preparing the update of", lobbyId, result));
                    return;
                }

                try
                {
                    foreach (var attribute in attributes)
                    {
                        var data = new AttributeData
                        {
                            Key = attribute.Key,
                            Value = attribute.IsText
                                ? new AttributeDataValue { AsUtf8 = attribute.Text }
                                : new AttributeDataValue { AsInt64 = attribute.Number },
                        };
                        if (member)
                        {
                            var addOptions = new LobbyModificationAddMemberAttributeOptions
                            {
                                Attribute = data,
                                Visibility = LobbyAttributeVisibility.Public,
                            };
                            result = modification.AddMemberAttribute(ref addOptions);
                        }
                        else
                        {
                            var addOptions = new LobbyModificationAddAttributeOptions
                            {
                                Attribute = data,
                                Visibility = LobbyAttributeVisibility.Public,
                            };
                            result = modification.AddAttribute(ref addOptions);
                        }

                        if (result != Result.Success)
                        {
                            done("setting " + attribute + " on the EOS lobby id=" + lobbyId + " failed: " + result);
                            return;
                        }
                    }

                    var updateOptions = new UpdateLobbyOptions { LobbyModificationHandle = modification };
                    _owner._lobby.UpdateLobby(ref updateOptions, null, (ref UpdateLobbyCallbackInfo info) =>
                        done(info.ResultCode == Result.Success ? null : Failure("updating", lobbyId, info.ResultCode)));
                }
                finally
                {
                    // UpdateLobby は呼んだ時点で変更を写し取るので、完了を待たずに解放してよい
                    modification.Release();
                }
            }

            // 外側が捨てられた後も入ったままのロビーから出る要求を送れるよう、捨てられたかは見ない。返事を捨てるかは MemberLobbies が決める。
            private bool Ready(out string error)
            {
                if (_owner._localUserId == null)
                {
                    error = "not logged in to EOS";
                    return false;
                }

                if (!_owner.PlatformAlive)
                {
                    error = "the EOS platform was released";
                    return false;
                }

                error = null;
                return true;
            }

            // ロビーが無い・入っていないときは決まった符号にする。MemberLobbies がそれを「もう出ている」として見分けるため。
            private static string Failure(string action, string lobbyId, Result result)
            {
                return result == Result.NotFound
                    ? LobbyBackendErrors.NotFound
                    : action + " the EOS member lobby id=" + lobbyId + " failed: " + result;
            }
        }
    }
}
