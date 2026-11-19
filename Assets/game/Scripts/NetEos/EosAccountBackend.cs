using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Runtime;
using UnityEngine;
// Auth と Connect の名前空間を両方開くと LoginOptions・Credentials などがぶつかるので、Connect は別名で参照する
using EosConnect = Epic.OnlineServices.Connect;

namespace Shooter.NetEos
{
    // 入ったユーザーは EosPlatform に覚えさせ、ロビー・パーティ・クラウドセーブが同じユーザーを使う。
    internal sealed class EosAccountBackend : IAccountBackend
    {
        private readonly IEosClientTransportFactory _factory;
        private EosAccountLoginSettings _settings;
        private Epic.OnlineServices.Platform.PlatformInterface _platform;
        private EosConnect.ConnectInterface _connect;
        private AuthInterface _auth;
        private EosDeviceLogin _deviceLogin;
        private EosEpicLogin _epicLogin;
        private Epic.OnlineServices.Platform.PlatformInterface _watchedPlatform;
        private EosConnect.ConnectInterface _watchedConnect;
        private ulong _authExpirationNotification;
        private ulong _loginStatusNotification;
        private IEosLogin _refresh;

        public EosAccountBackend(IEosClientTransportFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public AccountKind SignedInKind => PlatformAlive ? EosPlatform.LocalAccountKind : AccountKind.None;

        // プラットフォームは終了時に放されるので、作った時のものが今も同じかで確かめる。
        private bool PlatformAlive => _platform != null && ReferenceEquals(EosPlatform.Current, _platform);

        public bool TryPrepare(out bool epicAccount, out string unavailable)
        {
            epicAccount = false;
            if (!_factory.TryLoadCredentials(out var credentials, out var missing, out var loadError))
            {
                unavailable = "cannot load EOS credentials: " + loadError;
                return false;
            }

            if (credentials == null)
            {
                unavailable = "no EOS credentials; missing " + string.Join(", ", missing ?? Array.Empty<string>());
                return false;
            }

            _settings = credentials.AccountLogin;
            if (_settings.Problem != null)
            {
                Debug.LogWarning(AccountLog.SettingsProblem(_settings.Problem));
            }

            // どちらも無ければ起動時に入る理由が無いので、今までどおりロビーを開いたときに Device ID で入る。
            if (!credentials.HasEncryptionKey && !_settings.EpicAccount)
            {
                unavailable = "neither " + EosCredentials.EncryptionKeyVariable + " nor "
                    + EosAccountLoginSettings.EpicAccountLoginVariable + " is set, so nothing needs a sign-in at startup";
                return false;
            }

            var platform = EosPlatform.Acquire(credentials, out var acquireError);
            if (platform == null)
            {
                unavailable = acquireError;
                return false;
            }

            var connect = platform.GetConnectInterface();
            if (connect == null)
            {
                unavailable = "EOS Connect interface is unavailable";
                return false;
            }

            _platform = platform;
            _connect = connect;
            if (_settings.EpicAccount)
            {
                _auth = platform.GetAuthInterface();
                if (_auth == null)
                {
                    Debug.LogWarning(AccountLog.SettingsProblem("EOS Auth interface is unavailable"));
                }
            }

            epicAccount = _auth != null;
            unavailable = null;
            return true;
        }

        public void Tick()
        {
            if (!PlatformAlive)
            {
                return;
            }

            WatchLogin();
            EosPlatform.Tick();
        }

        public void SignInDevice(Action<AccountLoginResult> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!PlatformAlive)
            {
                done(AccountLoginResult.Failed("the EOS platform is gone"));
                return;
            }

            _deviceLogin?.Cancel();
            var login = new EosDeviceLogin(_connect, EosTransportInstaller.LobbyDisplayName, SystemInfo.deviceModel);
            _deviceLogin = login;
            login.Begin((userId, error) =>
            {
                if (ReferenceEquals(_deviceLogin, login))
                {
                    _deviceLogin = null;
                }

                done(error == null ? AccountLoginResult.SignedIn() : AccountLoginResult.Failed(error));
            });
        }

        public void RestoreEpic(Action<AccountLoginResult> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!TryStartEpic(out var login, out var unavailable))
            {
                done(AccountLoginResult.Failed(unavailable));
                return;
            }

            login.Restore(result => FinishEpic(login, result, done));
        }

        public void SignInEpic(Action<AccountLoginResult> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!TryStartEpic(out var login, out var unavailable))
            {
                done(AccountLoginResult.Failed(unavailable));
                return;
            }

            login.SignIn(result => FinishEpic(login, result, done));
        }

        public void SignOutEpic(Action<string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            _epicLogin?.Cancel();
            _epicLogin = null;
            _refresh?.Cancel();
            _refresh = null;

            var epicAccountId = EosPlatform.LocalEpicAccountId;
            var userId = EosPlatform.LocalUserId;

            // Auth から抜けるのを待たずに外すのは、抜けきれなくても次の入り直しで Device ID のユーザーへ移れるようにするため。
            if (EosPlatform.LocalAccountKind == AccountKind.Epic)
            {
                EosPlatform.ForgetLogin(userId);
            }

            if (_auth == null || !PlatformAlive)
            {
                done(null);
                return;
            }

            var errors = new List<string>();
            DeleteRememberedLogin(errors, () =>
            {
                if (epicAccountId == null)
                {
                    done(errors.Count == 0 ? null : string.Join("; ", errors));
                    return;
                }

                var options = new LogoutOptions { LocalUserId = epicAccountId };
                _auth.Logout(ref options, null, (ref LogoutCallbackInfo info) =>
                {
                    if (info.ResultCode != Result.Success)
                    {
                        errors.Add("EOS auth logout failed: " + info.ResultCode);
                    }

                    done(errors.Count == 0 ? null : string.Join("; ", errors));
                });
            });
        }

        private void DeleteRememberedLogin(List<string> errors, Action next)
        {
            var options = new DeletePersistentAuthOptions();
            _auth.DeletePersistentAuth(ref options, null, (ref DeletePersistentAuthCallbackInfo info) =>
            {
                // 覚えているトークンが元から無ければ NotFound で返る。
                if (info.ResultCode != Result.Success && info.ResultCode != Result.NotFound)
                {
                    errors.Add("deleting the remembered Epic login failed: " + info.ResultCode);
                }

                next();
            });
        }

        private bool TryStartEpic(out EosEpicLogin login, out string unavailable)
        {
            login = null;
            if (_auth == null)
            {
                unavailable = "Epic account sign-in is not set up (" + EosAccountLoginSettings.EpicAccountLoginVariable + ")";
                return false;
            }

            if (!PlatformAlive)
            {
                unavailable = "the EOS platform is gone";
                return false;
            }

            _epicLogin?.Cancel();
            login = new EosEpicLogin(_auth, _connect, _settings);
            _epicLogin = login;
            unavailable = null;
            return true;
        }

        private void FinishEpic(EosEpicLogin login, AccountLoginResult result, Action<AccountLoginResult> done)
        {
            if (ReferenceEquals(_epicLogin, login))
            {
                _epicLogin = null;
            }

            done(result);
        }

        // ロビーが先にログインしていても、ロビーを閉じると通知が外れるので、ここでも自分で見張る。
        private void WatchLogin()
        {
            if (ReferenceEquals(_platform, _watchedPlatform))
            {
                return;
            }

            _watchedPlatform = _platform;
            _refresh?.Cancel();
            _refresh = null;
            _watchedConnect = _connect;
            var expirationOptions = new EosConnect.AddNotifyAuthExpirationOptions();
            _authExpirationNotification = _watchedConnect.AddNotifyAuthExpiration(ref expirationOptions, null, HandleAuthExpiration);
            var statusOptions = new EosConnect.AddNotifyLoginStatusChangedOptions();
            _loginStatusNotification = _watchedConnect.AddNotifyLoginStatusChanged(ref statusOptions, null, HandleLoginStatusChanged);
            if (_authExpirationNotification == 0 || _loginStatusNotification == 0)
            {
                Debug.LogWarning(AccountLog.NotWatched());
            }
        }

        private void HandleAuthExpiration(ref EosConnect.AuthExpirationCallbackInfo info)
        {
            if (_refresh != null || !EosPlatform.IsLocalUser(info.LocalUserId) || !PlatformAlive)
            {
                return;
            }

            var kind = EosPlatform.LocalAccountKind;
            var refresh = EosPlatform.CreateRefresh(_watchedConnect, info.LocalUserId, EosTransportInstaller.LobbyDisplayName, SystemInfo.deviceModel);
            _refresh = refresh;
            refresh.Begin((userId, error) =>
            {
                if (ReferenceEquals(_refresh, refresh))
                {
                    _refresh = null;
                }

                if (error != null)
                {
                    // 期限が切れればログインの状態の通知が来て、AccountSignIn が入り直す。
                    Debug.LogWarning(AccountLog.RefreshFailed(kind, error));
                    return;
                }

                Debug.Log(AccountLog.Refreshed(kind));
            });
        }

        private void HandleLoginStatusChanged(ref EosConnect.LoginStatusChangedCallbackInfo info)
        {
            if (info.CurrentStatus == LoginStatus.LoggedIn || !EosPlatform.IsLocalUser(info.LocalUserId))
            {
                return;
            }

            EosPlatform.ForgetLogin(info.LocalUserId);
            _refresh?.Cancel();
            _refresh = null;
        }
    }
}
