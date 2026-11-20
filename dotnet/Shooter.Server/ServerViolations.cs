using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 時刻を tick の秒数の和で数えるのは、ServerAntiCheat と同じくテストから時間を進められるようにするため。
    internal sealed class ServerViolations : IServerModule
    {
        private readonly ServerCheatSignals _signals;
        private readonly AntiCheatTuning.ViolationSettings _settings;
        private readonly Dictionary<int, Window> _windows = new Dictionary<int, Window>();
        // 切るのは今の tick の後なので、それまでに届いた出来事で同じ人を何度も切らないよう覚えておく
        private readonly HashSet<int> _kicked = new HashSet<int>();
        private ServerContext _context;
        private bool _listening;
        private double _now;

        public ServerViolations(ServerCheatSignals signals, AntiCheatTuning.ViolationSettings settings)
        {
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string Name => "violations";

        public bool Enabled => _settings.Enabled;

        internal AntiCheatTuning.ViolationSettings Settings => _settings;

        internal ServerCheatSignals CheatSignals => _signals;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (!Enabled)
            {
                context.Log.WriteLine(ServerViolationLog.Off());
                return;
            }

            _signals.ShotRejected += HandleShotRejected;
            _signals.InputOverBudget += HandleInputOverBudget;
            _listening = true;
            context.Log.WriteLine(ServerViolationLog.On(_settings));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
            if (ServerMatch.TryGetPlayerId(connection, out var playerId))
            {
                _windows.Remove(playerId);
                _kicked.Remove(playerId);
            }
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            if (_listening)
            {
                _signals.ShotRejected -= HandleShotRejected;
                _signals.InputOverBudget -= HandleInputOverBudget;
                _listening = false;
            }

            _windows.Clear();
            _kicked.Clear();
        }

        // ダウン中・倒れている人の射撃は戦闘が理由を出さずに捨て、ここへは届かないので、理由だけで回避・乗り越えの最中を除く
        internal static bool IsCounted(ShotRejection reason)
        {
            return reason != ShotRejection.Dodging && reason != ShotRejection.Vaulting;
        }

        private void HandleShotRejected(int playerId, ShotRejection reason)
        {
            if (IsCounted(reason))
            {
                Count(playerId, ServerViolationKind.ShotRejected, _settings.ShotRejectionLimit);
            }
        }

        private void HandleInputOverBudget(int playerId)
        {
            Count(playerId, ServerViolationKind.InputOverBudget, _settings.OverBudgetLimit);
        }

        private void Count(int playerId, ServerViolationKind kind, int limit)
        {
            if (BotRoster.IsBotId(playerId) || _kicked.Contains(playerId))
            {
                return;
            }

            // 抜けた後に届いた出来事で、もういない人の数を作り直さない
            if (!_context.Match.TryGetPlayer(playerId, out var player) || !player.Connection.IsValid)
            {
                return;
            }

            if (!_windows.TryGetValue(playerId, out var window))
            {
                window = new Window();
                _windows.Add(playerId, window);
            }

            var times = window.For(kind);
            var since = _now - _settings.WindowSeconds;
            while (times.Count > 0 && times.Peek() <= since)
            {
                times.Dequeue();
            }

            times.Enqueue(_now);
            if (times.Count > limit)
            {
                Kick(player, kind, times.Count, limit);
            }
        }

        private void Kick(ServerPlayer player, ServerViolationKind kind, int count, int limit)
        {
            var window = _settings.WindowSeconds;
            _context.Log.WriteLine(ServerViolationLog.Kicking(player.PlayerId, kind, count, limit, window));
            _context.Control.Kick(
                player.Connection,
                DisconnectNotice.RejectedReason(AntiCheatSettings.RejectedDetail),
                ServerViolationLog.KickText(kind, count, limit, window));
            _windows.Remove(player.PlayerId);
            _kicked.Add(player.PlayerId);
        }

        // 列の長さに上限を設けないのは、上限を超えた時点で切って捨てるので、上限 + 1 より長くならないため
        private sealed class Window
        {
            private readonly Queue<double> _shotRejections = new Queue<double>();
            private readonly Queue<double> _overBudget = new Queue<double>();

            public Queue<double> For(ServerViolationKind kind)
            {
                return kind == ServerViolationKind.ShotRejected ? _shotRejections : _overBudget;
            }
        }
    }
}
