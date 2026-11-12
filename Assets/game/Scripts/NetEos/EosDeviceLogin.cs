using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;

namespace Fps.NetEos
{
    /// <summary>
    /// Connect インターフェースに Device ID でログインし、P2P に使う ProductUserId を得る。
    /// Epic アカウントを持たない専用サーバー・ヘッドレスのクライアントでも使える。
    /// 端末ごとに同じ ID になるので、同じ PC・同じ OS ユーザーで起動したサーバーとクライアントは互いにつながらない。
    /// </summary>
    internal sealed class EosDeviceLogin
    {
        /// <summary>EOS が受け付ける表示名の最大文字数。</summary>
        private const int _maxDisplayNameLength = 32;

        private readonly ConnectInterface _connect;
        private readonly string _displayName;
        private readonly string _deviceModel;
        private Action<ProductUserId, string> _done;

        /// <param name="connect">プラットフォームの Connect インターフェース。</param>
        /// <param name="displayName">初めてログインしたときに作るユーザーの表示名。</param>
        /// <param name="deviceModel">Device ID を作るときの端末名。</param>
        public EosDeviceLogin(ConnectInterface connect, string displayName, string deviceModel)
        {
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _displayName = Truncate(displayName, _maxDisplayNameLength);
            _deviceModel = string.IsNullOrEmpty(deviceModel) ? "unknown" : deviceModel;
        }

        /// <summary>
        /// ログインを始める。終わったら <paramref name="done"/> を、成功なら (ID, null)、失敗なら (null, 理由) で 1 回呼ぶ。
        /// コールバックは <see cref="EosPlatform.Tick"/> の中で呼ばれる。
        /// </summary>
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

        /// <summary>結果を受け取らないようにする。ログイン中に transport を止めたときに呼ぶ。</summary>
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

            done(userId, error);
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "fps";
            }

            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }
    }
}
