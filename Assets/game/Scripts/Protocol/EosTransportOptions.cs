using System;

namespace Shooter.Protocol
{
    public sealed class EosTransportOptions
    {
        // EOS はログインから P2P がつながるまで UDP より時間がかかるので、20 秒待つ。
        public const int DefaultConnectTimeoutMilliseconds = 20000;

        // サーバーは待ち受けを始める前にログインを済ませるので、つなぐ側より長く待つ。
        public const int DefaultSignInTimeoutMilliseconds = 30000;

        // EOS は閉じると送りかけのパケットを捨てうるので、切断の知らせを送ってもこちらからはすぐに閉じない。
        public const int DefaultCloseLingerMilliseconds = 3000;

        // 止めるのを長く引き延ばさないよう、止めるときは切るときより短く待つ。
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
