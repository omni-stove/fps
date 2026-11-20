using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // MatchClient のある Shooter.NetV2.Client は Shooter.Runtime に依存しないので、回線の値を GameSignals に出すのはこの部品が受け持つ。
    public sealed class NetV2NetStatsPart : INetV2ClientPart
    {
        public const float DefaultIntervalSeconds = 1f;

        private readonly MatchClient _client;
        private readonly float _intervalSeconds;
        private float _untilNextSeconds;
        private int? _snapshotTick;
        private NetStatsSignal? _published;
        private bool _disposed;

        public NetV2NetStatsPart(MatchClient client, float intervalSeconds = DefaultIntervalSeconds)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (!(intervalSeconds > 0f) || float.IsInfinity(intervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "the interval must be positive and finite");
            }

            _intervalSeconds = intervalSeconds;
            _client.Disconnected += HandleDisconnected;
        }

        public NetStatsSignal? Published => _published;

        public bool Handle(IMessage message)
        {
            return false;
        }

        // 承認の tick は承認の時点で止まるので、届いたスナップショットの tick があればそちらをサーバーの今の tick として出す。
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            _snapshotTick = Math.Max(0, snapshot.Tick);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            // Leave で抜けたときは Disconnected が来ないので、毎回の Tick で状態を確かめて空に戻す。
            if (_client.State != MatchClientState.Connected)
            {
                Withdraw();
                return;
            }

            _untilNextSeconds -= deltaSeconds;
            if (_published.HasValue && _untilNextSeconds > 0f)
            {
                return;
            }

            _untilNextSeconds = _intervalSeconds;
            var stats = Sample();
            if (_published == stats)
            {
                return;
            }

            _published = stats;
            GameSignals.NetStats.Publish(stats);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _client.Disconnected -= HandleDisconnected;
            Withdraw();
            _disposed = true;
        }

        private NetStatsSignal Sample()
        {
            int? ping = _client.TryGetRoundTripTime(out var milliseconds) ? Math.Max(0, milliseconds) : (int?)null;
            int? loss = _client.TryGetLossPercent(out var percent)
                ? Math.Max(0, Math.Min(NetStatsSignal.MaxLossPercent, percent))
                : (int?)null;
            var tick = _snapshotTick ?? Math.Max(0, _client.AcceptedServerTick);
            return new NetStatsSignal(ping, loss, tick);
        }

        private void HandleDisconnected(DisconnectNotice notice)
        {
            Withdraw();
        }

        private void Withdraw()
        {
            _snapshotTick = null;
            _untilNextSeconds = 0f;
            if (!_published.HasValue)
            {
                return;
            }

            GameSignals.NetStats.ClearIfCurrent(_published);
            _published = null;
        }
    }
}
