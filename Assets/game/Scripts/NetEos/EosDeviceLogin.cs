using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;

namespace Shooter.NetEos
{
    // Epic アカウントでなく Device ID でログインするのは、アカウントを持たない専用サーバー・ヘッドレスのクライアントでも使えるようにするため。
    // 端末ごとに同じ ID になるので、同じ PC・同じ OS ユーザーで起動したサーバーとクライアントは互いにつながらない。
    internal sealed class EosDeviceLogin : IEosLogin
    {
        // EOS が受け付ける表示名の最大文字数。
        private const int _maxDisplayNameLength = 32;

        private readonly ConnectInterface _connect;
        private readonly string _displayName;
        private readonly string _deviceModel;
        private Action<ProductUserId, string> _done;

        public EosDeviceLogin(ConnectInterface connect, string displayName, string deviceModel)
        {
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _displayName = Truncate(displayName, _maxDisplayNameLength);
            _deviceModel = string.IsNullOrEmpty(deviceModel) ? "unknown" : deviceModel;
        }

        public void Begin(Action<ProductUserId, string> done)
        {
            if (_done != null)
            {
                throw new InvalidOperationException("login already started");
            }

            _done = done ?? throw new ArgumentNullException(nameof(done));
            var options = new CreateDeviceIdOptions { DeviceModel = _deviceModel };
            _connect.CreateDeviceId(ref options, null, HandleDeviceIdCreated);
        }

        public void Cancel()
        {
            _done = null;
        }

        private void HandleDeviceIdCreated(ref CreateDeviceIdCallbackInfo info)
        {
            if (_done == null)
            {
                return;
            }

            // 2 回目以降は既に Device ID があるので DuplicateNotAllowed になる
            if (info.ResultCode != Result.Success && info.ResultCode != Result.DuplicateNotAllowed)
            {
                Finish(null, "creating the EOS device id failed: " + info.ResultCode);
                return;
            }

            var options = new LoginOptions
            {
                Credentials = new Credentials
                {
                    Type = ExternalCredentialType.DeviceidAccessToken,
                    Token = null,
                },
                UserLoginInfo = new UserLoginInfo { DisplayName = _displayName },
            };
            _connect.Login(ref options, null, HandleLogin);
        }

        private void HandleLogin(ref LoginCallbackInfo info)
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

            // この Device ID に紐づくユーザーがまだ無い。続きのトークンで作る
            if (info.ResultCode == Result.InvalidUser && info.ContinuanceToken != null)
            {
                var options = new CreateUserOptions { ContinuanceToken = info.ContinuanceToken };
                _connect.CreateUser(ref options, null, HandleUserCreated);
                return;
            }

            Finish(null, "EOS connect login failed: " + info.ResultCode);
        }

        private void HandleUserCreated(ref CreateUserCallbackInfo info)
        {
            if (info.ResultCode != Result.Success)
            {
                Finish(null, "creating the EOS user failed: " + info.ResultCode);
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

            // 別のユーザー（Epic アカウント）が覚えられている間は、Device ID で入り直しても上書きしない。
            if (error == null && (EosPlatform.LocalUserId == null || EosPlatform.IsLocalUser(userId)))
            {
                EosPlatform.RememberLogin(userId);
            }

            done(userId, error);
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "shooter";
            }

            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }
    }
}
