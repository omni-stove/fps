using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 要求された transport と手元の認証情報から、実際に使う transport を決めた結果。
    /// EOS を要求されても認証情報が揃わなければ UDP (従来の直接接続) に切り替える。
    /// </summary>
    public sealed class NetTransportPlan
    {
        private static readonly IReadOnlyList<string> _none = Array.Empty<string>();

        /// <summary>実際に使う設定。UDP に切り替えたときは transport だけを差し替えたもの。</summary>
        public NetLaunchOptions Options { get; }

        /// <summary>EOS を使うときの認証情報。UDP では null。</summary>
        public EosCredentials Credentials { get; }

        /// <summary>EOS を要求されたが認証情報が無く、UDP に切り替えたか。</summary>
        public bool FellBackToUdp { get; }

        /// <summary>切り替えの原因になった、見つからなかった認証情報の名前。切り替えていなければ空。</summary>
        public IReadOnlyList<string> MissingCredentials { get; }

        private NetTransportPlan(
            NetLaunchOptions options,
            EosCredentials credentials,
            bool fellBackToUdp,
            IReadOnlyList<string> missingCredentials)
        {
            Options = options;
            Credentials = credentials;
            FellBackToUdp = fellBackToUdp;
            MissingCredentials = missingCredentials;
        }

        /// <param name="requested">コマンドライン引数から作った設定。</param>
        /// <param name="credentials">揃った認証情報。揃っていなければ null。</param>
        /// <param name="missingCredentials">見つからなかった認証情報の名前。null なら空とみなす。</param>
        public static NetTransportPlan Decide(
            NetLaunchOptions requested,
            EosCredentials credentials,
            IReadOnlyList<string> missingCredentials)
        {
            if (requested == null)
            {
                throw new ArgumentNullException(nameof(requested));
            }

            if (requested.Transport == NetTransportKind.Udp)
            {
                return new NetTransportPlan(requested, null, false, _none);
            }

            if (credentials != null)
            {
                return new NetTransportPlan(requested, credentials, false, _none);
            }

            return new NetTransportPlan(
                requested.WithTransport(NetTransportKind.Udp),
                null,
                true,
                missingCredentials ?? _none);
        }
    }
}
