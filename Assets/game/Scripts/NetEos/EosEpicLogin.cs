using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Shooter.Core;
using EosConnect = Epic.OnlineServices.Connect;

namespace Shooter.NetEos
{
    // Auth で Epic アカウントに入り、続けて EosEpicConnectLogin で Connect に入る。
    // 起動時は SDK が覚えている更新トークン (PersistentAuth) で画面を出さずに入り、
    // プレイヤーが選んだときだけ AccountPortal (開発中は DevAuthTool の Developer) で入る。
    internal sealed class EosEpicLogin
    {
        private readonly AuthInterface _auth;
        private readonly EosConnect.ConnectInterface _connect;
        private readonly EosAccountLoginSettings _settings;
        private Action<AccountLoginResult> _done;
        private EosEpicConnectLogin _connectLogin;
        private bool _restoring;

        public EosEpicLogin(AuthInterface auth, EosConnect.ConnectInterface connect, EosAccountLoginSettings settings)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Restore(Action<AccountLoginResult> done)
        {
            Start(done, true);

            // Connect の期限だけが切れて入り直すときは、Auth にはまだ入っているので、トークンを読み直さずに Connect から入る。
            var signedIn = EosPlatform.LocalEpicAccountId ?? FirstLoggedInAccount();
            if (signedIn != null)
            {
                ContinueWithConnect(signedIn);
                return;
            }

            var credentials = new Credentials { Type = LoginCredentialType.PersistentAuth };
            BeginAuth(credentials);
        }

        public void SignIn(Action<AccountLoginResult> done)
        {
            Start(done, false);
            var credentials = _settings.HasDevAuth
                ? new Credentials { Type = LoginCredentialType.Developer, Id = _settings.DevAuthHost, Token = _settings.DevAuthCredential }
                : new Credentials { Type = LoginCredentialType.AccountPortal };
            BeginAuth(credentials);
        }

        public void Cancel()
        {
            _done = null;
            _connectLogin?.Cancel();
            _connectLogin = null;
        }

        private void Start(Action<AccountLoginResult> done, bool restoring)
        {
            if (_done != null)
            {
                throw new InvalidOperationException("login already started");
            }

            _done = done ?? throw new ArgumentNullException(nameof(done));
            _restoring = restoring;
        }

        private void BeginAuth(Credentials credentials)
        {
            var options = new LoginOptions
            {
                Credentials = credentials,
                ScopeFlags = AuthScopeFlags.BasicProfile,
            };
            _auth.Login(ref options, null, HandleAuthLogin);
        }

        private void HandleAuthLogin(ref LoginCallbackInfo info)
        {
            if (_done == null)
            {
                return;
            }

            if (info.ResultCode == Result.Success && info.LocalUserId != null && info.LocalUserId.IsValid())
            {
                ContinueWithConnect(info.LocalUserId);
                return;
            }

            if (_restoring && IsMissingOrStale(info.ResultCode))
            {
                // 期限が切れた・取り消された更新トークンは何度使っても通らないので、次の起動で試さないよう消す。
                if (info.ResultCode != Result.NotFound)
                {
                    DeletePersistentAuth();
                }

                Finish(AccountLoginResult.NoSavedLogin("EOS auth persistent login returned " + info.ResultCode));
                return;
            }

            if (info.ResultCode == Result.Canceled)
            {
                Finish(AccountLoginResult.Cancelled());
                return;
            }

            Finish(AccountLoginResult.Failed("EOS auth login failed: " + info.ResultCode));
        }

        private void ContinueWithConnect(EpicAccountId epicAccountId)
        {
            _connectLogin = new EosEpicConnectLogin(_auth, _connect, epicAccountId);
            _connectLogin.Begin((userId, error) =>
            {
                _connectLogin = null;
                Finish(error == null ? AccountLoginResult.SignedIn() : AccountLoginResult.Failed(error));
            });
        }

        private EpicAccountId FirstLoggedInAccount()
        {
            var count = _auth.GetLoggedInAccountsCount();
            for (var i = 0; i < count; i++)
            {
                var account = _auth.GetLoggedInAccountByIndex(i);
                if (account != null && account.IsValid() && _auth.GetLoginStatus(account) == LoginStatus.LoggedIn)
                {
                    return account;
                }
            }

            return null;
        }

        private void DeletePersistentAuth()
        {
            var options = new DeletePersistentAuthOptions();
            _auth.DeletePersistentAuth(ref options, null, (ref DeletePersistentAuthCallbackInfo _) => { });
        }

        private void Finish(AccountLoginResult result)
        {
            var done = _done;
            _done = null;
            done?.Invoke(result);
        }

        private static bool IsMissingOrStale(Result result)
        {
            return result == Result.NotFound
                || result == Result.InvalidAuth
                || result == Result.AuthExpired
                || result == Result.InvalidCredentials
                || result == Result.InvalidUser
                || result == Result.AccessDenied;
        }
    }
}
