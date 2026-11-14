#if EOS_SDK
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Fps.Core;
// Lobby と Connect の名前空間を両方開くと型名がぶつかりうるので、Connect は別名で参照する
using EosConnect = Epic.OnlineServices.Connect;
// SDK の名前空間に同名の型が増えても、こちらを指すようにする
using LobbyAttribute = Fps.Core.LobbyAttribute;
using LobbyInfo = Fps.Core.LobbyInfo;

namespace Fps.Server.Eos
{
    /// <summary>
    /// Unity の <c>Fps.NetEos.EosLobbyBackend</c> のうち、サーバーのロビーに要る部分を EOS の C# SDK で直接行う。
    /// </summary>
    /// <remarks>
    /// ロビーは公開 (検索で見つかる)・プレゼンス無し・ID での参加を許可・ホスト移行なしで作る。
    /// 属性は作成の直後に公開の属性として付け、付けられなければロビーを閉じて失敗にする。
    /// Connect の認証の期限が近づいたら Device ID でログインし直し、ログインが切れたら <see cref="LoggedOut"/> で知らせる。
    /// </remarks>
    internal sealed class EosLobbyBackend : IServerLobbyBackend
    {
        private readonly EosCredentials _credentials;
        private readonly string _version;
        private readonly string _displayName;
        private readonly TextWriter _log;
        private EosServerPlatform _platform;
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

        public EosLobbyBackend(EosCredentials credentials, string version, string displayName, TextWriter log)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
            _displayName = string.IsNullOrWhiteSpace(displayName) ? ServerLobby.DisplayName : displayName;
        }

        public event Action<string, string> Removed;

        public event Action<string> LoggedOut;

        private bool PlatformAlive => _platform != null && _platform.IsAlive;

        private static string DeviceModel => "fps-server " + RuntimeInformation.RuntimeIdentifier;

        public void Tick()
        {
            if (!_disposed && PlatformAlive)
            {
                _platform.Tick();
            }
        }

        public void Login(Action<string, string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_login != null || _localUserId != null)
            {
                throw new InvalidOperationException("already logged in or logging in");
            }

            if (_platform == null)
            {
                // 初期化に失敗したら、次のログインでやり直せるよう持たない
                var platform = EosServerPlatform.Acquire(_credentials, _version, _log, out var error);
                if (platform == null)
                {
                    done(null, error);
                    return;
                }

                var lobby = platform.Interface.GetLobbyInterface();
                var connect = platform.Interface.GetConnectInterface();
                if (lobby == null || connect == null)
                {
                    platform.Release();
                    done(null, "EOS Lobby or Connect interface is unavailable");
                    return;
                }

                _platform = platform;
                _lobby = lobby;
                _connect = connect;
            }

            var loggedIn = _platform.LocalUserId;
            if (loggedIn != null && loggedIn.IsValid())
            {
                FinishLogin(loggedIn, null, done);
                return;
            }

            _login = new EosDeviceLogin(_connect, _displayName, DeviceModel);
            _login.Begin((userId, loginError) =>
            {
                _login = null;
                FinishLogin(userId, loginError, done);
            });
        }

        public void CreateLobby(string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            ArgumentNullException.ThrowIfNull(attributes);
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
                    EnteredLobby(lobbyId, done);
                    return;
                }

                // CreateLobby は属性を受け取らないので、作った直後に足す
                ApplyAttributes(lobbyId, attributes, attributeError =>
                {
                    if (attributeError == null)
                    {
                        EnteredLobby(lobbyId, done);
                        return;
                    }

                    // 属性の無いサーバーのロビーは検索で選ばれても使えないので残さない
                    if (PlatformAlive)
                    {
                        SendDestroy(lobbyId, _ => { });
                    }

                    Complete(done, null, attributeError);
                });
            });
        }

        public void UpdateAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            ArgumentNullException.ThrowIfNull(attributes);
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

        public void DestroyLobby(string lobbyId, Action<string> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            if (string.Equals(_currentLobbyId, lobbyId, StringComparison.Ordinal))
            {
                _currentLobbyId = null;
            }

            if (!CanCall(out var unavailable))
            {
                done(unavailable);
                return;
            }

            SendDestroy(lobbyId, done);
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
            Removed = null;
            LoggedOut = null;
            _platform?.Release();
            _platform = null;
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
                _log.WriteLine("[Lobby] EOS connect notifications could not be registered; the login will not be refreshed");
            }

            _localUserId = userId;
            _platform.LocalUserId = userId;
            done(userId.ToString(), null);
        }

        private void HandleAuthExpiration(ref EosConnect.AuthExpirationCallbackInfo info)
        {
            if (_disposed || _refresh != null || _localUserId == null || !IsLocalUser(info.LocalUserId) || !PlatformAlive)
            {
                return;
            }

            _refresh = new EosDeviceLogin(_connect, _displayName, DeviceModel);
            _refresh.Begin((userId, error) =>
            {
                _refresh = null;
                // 失敗しても、期限が切れればログインの状態の通知が来て LoggedOut からやり直しになる
                _log.WriteLine(error != null
                    ? "[Lobby] refreshing the EOS login failed: " + error
                    : "[Lobby] refreshed the EOS login user=" + userId);
            });
        }

        private void HandleLoginStatusChanged(ref EosConnect.LoginStatusChangedCallbackInfo info)
        {
            if (_disposed || _localUserId == null || !IsLocalUser(info.LocalUserId) || info.CurrentStatus == LoginStatus.LoggedIn)
            {
                return;
            }

            var reason = "the EOS connect login became " + info.CurrentStatus;
            if (PlatformAlive)
            {
                _platform.LocalUserId = null;
            }

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

        /// <summary>結果は (Dispose 後でも) 1 回だけ渡すので、呼び出し側が Dispose 後の扱いを決める。</summary>
        private void ApplyAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done)
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

        private void EnteredLobby(string lobbyId, Action<LobbyInfo, string> done)
        {
            if (_disposed)
            {
                if (PlatformAlive)
                {
                    SendDestroy(lobbyId, _ => { });
                }

                return;
            }

            var lobby = ReadLobby(lobbyId, out var error);
            if (lobby == null)
            {
                // 中身が読めないロビーは載っている属性も確かめられないので残さない
                SendDestroy(lobbyId, _ => { });
                done(null, error);
                return;
            }

            _currentLobbyId = lobbyId;
            done(lobby, null);
        }

        private void SendDestroy(string lobbyId, Action<string> done)
        {
            var options = new DestroyLobbyOptions { LobbyId = lobbyId, LocalUserId = _localUserId };
            _lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo info) =>
            {
                if (_disposed)
                {
                    return;
                }

                // 既に無いロビーを閉じるのは成功とみなす
                done(info.ResultCode == Result.Success || info.ResultCode == Result.NotFound
                    ? null
                    : "closing EOS lobby " + lobbyId + " failed: " + info.ResultCode);
            });
        }

        private void HandleMemberStatus(ref LobbyMemberStatusReceivedCallbackInfo info)
        {
            var lobbyId = Text(info.LobbyId);
            if (_disposed || _currentLobbyId == null || !string.Equals(lobbyId, _currentLobbyId, StringComparison.Ordinal))
            {
                return;
            }

            var isSelf = IsLocalUser(info.TargetUserId);
            var status = info.CurrentStatus;
            if (status == LobbyMemberStatus.Closed
                || (isSelf && (status == LobbyMemberStatus.Kicked
                    || status == LobbyMemberStatus.Left
                    || status == LobbyMemberStatus.Disconnected)))
            {
                _currentLobbyId = null;
                Removed?.Invoke(lobbyId, status == LobbyMemberStatus.Closed ? "the lobby was closed" : "this user was " + status);
            }
        }

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
            ObjectDisposedException.ThrowIf(_disposed, this);
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
#endif
