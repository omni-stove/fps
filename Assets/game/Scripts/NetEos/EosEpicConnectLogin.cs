using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
// Auth と Connect の名前空間を両方開くと LoginOptions・Credentials などがぶつかるので、Connect は別名で参照する
using EosConnect = Epic.OnlineServices.Connect;

namespace Shooter.NetEos
{
    // Auth で入った Epic アカウントの ID トークンを Connect に渡し、端末をまたいで同じ ProductUserId で入る。
    // Auth のトークンは SDK が自分で延ばすので、Connect の期限切れの入り直しにもそのまま使える。
    internal sealed class EosEpicConnectLogin : IEosLogin
    {
        private readonly AuthInterface _auth;
        private readonly EosConnect.ConnectInterface _connect;
        private readonly EpicAccountId _epicAccountId;
        private Action<ProductUserId, string> _done;

        public EosEpicConnectLogin(AuthInterface auth, EosConnect.ConnectInterface connect, EpicAccountId epicAccountId)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            if (epicAccountId == null || !epicAccountId.IsValid())
            {
                throw new ArgumentException("the Epic account id must be valid", nameof(epicAccountId));
            }

            _epicAccountId = epicAccountId;
        }

        public void Begin(Action<ProductUserId, string> done)
        {
            if (_done != null)
            {
                throw new InvalidOperationException("login already started");
            }

            _done = done ?? throw new ArgumentNullException(nameof(done));
            var copyOptions = new CopyIdTokenOptions { AccountId = _epicAccountId };
            var copied = _auth.CopyIdToken(ref copyOptions, out var idToken);
            if (copied != Result.Success || !idToken.HasValue)
            {
                Finish(null, "copying the Epic account id token failed: " + copied);
                return;
            }

            string token = idToken.Value.JsonWebToken;
            if (string.IsNullOrEmpty(token))
            {
                Finish(null, "the Epic account id token was empty");
                return;
            }

            // EpicIdToken では表示名を Epic アカウントから取るので、UserLoginInfo は渡さない。
            var options = new EosConnect.LoginOptions
            {
                Credentials = new EosConnect.Credentials
                {
                    Type = ExternalCredentialType.EpicIdToken,
                    Token = token,
                },
            };
            _connect.Login(ref options, null, HandleLogin);
        }

        public void Cancel()
        {
            _done = null;
        }

        private void HandleLogin(ref EosConnect.LoginCallbackInfo info)
        {
            if (_done == null)
            {
                return;
            }

            if (info.ResultCode == Result.Success)
            {
                Finish(info.LocalUserId, null);
                return;
            }

            // この Epic アカウントに紐づく EOS のユーザーがまだ無い。続きのトークンで作る
            if (info.ResultCode == Result.InvalidUser && info.ContinuanceToken != null)
            {
                var options = new EosConnect.CreateUserOptions { ContinuanceToken = info.ContinuanceToken };
                _connect.CreateUser(ref options, null, HandleUserCreated);
                return;
            }

            Finish(null, "EOS connect login with the Epic account failed: " + info.ResultCode);
        }

        private void HandleUserCreated(ref EosConnect.CreateUserCallbackInfo info)
        {
            if (info.ResultCode != Result.Success)
            {
                Finish(null, "creating the EOS user for the Epic account failed: " + info.ResultCode);
                return;
            }

            Finish(info.LocalUserId, null);
        }

        private void Finish(ProductUserId userId, string error)
        {
            var done = _done;
            _done = null;
            if (done == null)
            {
                return;
            }

            if (error == null && (userId == null || !userId.IsValid()))
            {
                error = "EOS returned an invalid product user id";
                userId = null;
            }

            if (error == null)
            {
                EosPlatform.RememberEpicLogin(userId, _epicAccountId);
            }

            done(userId, error);
        }
    }
}
