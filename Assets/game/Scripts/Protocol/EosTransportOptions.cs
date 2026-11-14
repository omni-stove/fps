using System;

namespace Fps.Protocol
{
    /// <summary><see cref="EosMatchTransport"/> の待ち時間の調整値。</summary>
    public sealed class EosTransportOptions
    {
        /// <summary>Netcode の EOS の transport (<c>EosP2PTransport.ConnectTimeoutSeconds</c>) と同じく、ログインからつながるまで 20 秒待つ。</summary>
        public const int DefaultConnectTimeoutMilliseconds = 20000;

        /// <summary>サーバーは待ち受けを始める前にログインを済ませるので、つなぐ側より長く待つ。</summary>
        public const int DefaultSignInTimeoutMilliseconds = 30000;

        /// <summary>
        /// 切断の知らせを送ってから、相手が受け取って閉じるのを待つ最長。EOS は閉じると送りかけのパケットを捨てうるので、
        /// こちらからはすぐに閉じない。
        /// </summary>
        public const int DefaultCloseLingerMilliseconds = 3000;

        /// <summary>止めるときに、切断の知らせが相手に届いて相手が閉じるのを待つ最長。止めるのを長く引き延ばさない。</summary>
        public const int DefaultShutdownLingerMilliseconds = 1000;

        public const int DefaultPingIntervalMilliseconds = 1000;

        private int _connectTimeoutMilliseconds = DefaultConnectTimeoutMilliseconds;
        private int _signInTimeoutMilliseconds = DefaultSignInTimeoutMilliseconds;
        private int _closeLingerMilliseconds = DefaultCloseLingerMilliseconds;
        private int _shutdownLingerMilliseconds = DefaultShutdownLingerMilliseconds;
        private int _pingIntervalMilliseconds = DefaultPingIntervalMilliseconds;

        public int ConnectTimeoutMilliseconds
        {
            get => _connectTimeoutMilliseconds;
            set => _connectTimeoutMilliseconds = Positive(value);
        }

        public int SignInTimeoutMilliseconds
        {
            get => _signInTimeoutMilliseconds;
            set => _signInTimeoutMilliseconds = Positive(value);
        }

        public int CloseLingerMilliseconds
        {
            get => _closeLingerMilliseconds;
            set => _closeLingerMilliseconds = NotNegative(value);
        }

        public int ShutdownLingerMilliseconds
        {
            get => _shutdownLingerMilliseconds;
            set => _shutdownLingerMilliseconds = NotNegative(value);
        }

        public int PingIntervalMilliseconds
        {
            get => _pingIntervalMilliseconds;
            set => _pingIntervalMilliseconds = Positive(value);
        }

        private static int Positive(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "value must be positive");
            }

            return value;
        }

        private static int NotNegative(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "value must not be negative");
            }

            return value;
        }
    }
}
