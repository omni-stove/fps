using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 射撃と視線の統計を人ごとに時間窓で集め、閾値を超えた指標をログに出すだけで、試合の状態は変えない (キックもしない)。
    // 時間窓は人ごとに区切り、窓が替わるまで同じ人の同じ指標は 1 回しか出さない。続けて超える人がログを埋めないため。
    // 見え始めは送り分け (conceal) と同じ ServerVisibility で決めるが、送り分けは見る側のまとまりで覚え、ここは撃った本人が見えたかを知りたいので、人ごとに覚える。
    internal sealed class ServerCheatStats : IServerModule
    {
        private const float _radiansToDegrees = 180f / MathF.PI;
        private const float _minAimDistance = 1e-4f;

        private readonly ServerCheatSignals _signals;
        private readonly AntiCheatTuning.StatsSettings _stats;
        private readonly AntiCheatTuning.ConcealSettings _conceal;
        private readonly Func<int, bool> _isHittable;
        private readonly Func<int, int, bool> _areHostile;
        private readonly float _tickSeconds;
        private readonly Dictionary<int, Watched> _players = new Dictionary<int, Watched>();
        private readonly Dictionary<(int Viewer, int Target), Sight> _sights = new Dictionary<(int Viewer, int Target), Sight>();
        private readonly List<(int Viewer, int Target)> _gone = new List<(int Viewer, int Target)>();
        private readonly List<ServerBody> _viewers = new List<ServerBody>();
        private readonly List<ServerBody> _targets = new List<ServerBody>();
        private ServerContext _context;
        private ServerVisibility _visibility;
        private double _now;
        private long _tick;
        private bool _collecting;

        public ServerCheatStats(ServerCheatSignals signals, AntiCheatTuning antiCheat, ServerCombat combat, int tickRate = TickClock.DefaultTickRate)
            : this(
                signals,
                antiCheat,
                (combat ?? throw new ArgumentNullException(nameof(combat))).IsHittable,
                combat.Rules.AreHostile,
                tickRate)
        {
        }

        // 生きているか・敵かは呼ぶ側 (戦闘とルール) が決める。ここで二重に持つと、ルールの替わり目で食い違うため
        public ServerCheatStats(
            ServerCheatSignals signals,
            AntiCheatTuning antiCheat,
            Func<int, bool> isHittable,
            Func<int, int, bool> areHostile,
            int tickRate = TickClock.DefaultTickRate)
        {
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            ArgumentNullException.ThrowIfNull(antiCheat);
            _stats = antiCheat.Stats;
            _conceal = antiCheat.Conceal;
            _isHittable = isHittable ?? throw new ArgumentNullException(nameof(isHittable));
            _areHostile = areHostile ?? throw new ArgumentNullException(nameof(areHostile));
            if (tickRate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be at least 1");
            }

            _tickSeconds = 1f / tickRate;
        }

        public string Name => "cheat-stats";

        public AntiCheatTuning.StatsSettings Stats => _stats;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (!_stats.Enabled)
            {
                return;
            }

            _collecting = true;
            _visibility = new ServerVisibility(context.World, _conceal);
            _signals.ShotAccepted += HandleShotAccepted;
            _signals.Hit += HandleHit;
            _signals.Looked += HandleLooked;
            context.Match.PlayerLeft += HandlePlayerLeft;
            context.Match.MapChanged += HandleMapChanged;
            context.Bodies.Teleported += HandleTeleported;
            context.Log.WriteLine(ServerCheatStatsLog.Started(_stats));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            if (!_collecting)
            {
                return;
            }

            _now += deltaSeconds;
            _tick = tick;
            foreach (var player in _players.Values)
            {
                Roll(player);
            }

            Observe(tick);
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            if (!_collecting)
            {
                return;
            }

            _collecting = false;
            _signals.ShotAccepted -= HandleShotAccepted;
            _signals.Hit -= HandleHit;
            _signals.Looked -= HandleLooked;
            _context.Match.PlayerLeft -= HandlePlayerLeft;
            _context.Match.MapChanged -= HandleMapChanged;
            _context.Bodies.Teleported -= HandleTeleported;
            _players.Clear();
            _sights.Clear();
        }

        private void HandleShotAccepted(int shooterId, Vector3 origin, Vector3 direction, float shotTick)
        {
            if (!TryWatch(shooterId, out var player))
            {
                return;
            }

            player.ShotSerial++;
        }

        private void HandleHit(int shooterId, int victimId, HitZone zone, float rewindTicks)
        {
            if (shooterId == victimId || !TryWatch(shooterId, out var player))
            {
                return;
            }

            CountHeadshot(player, victimId, zone);
            CountReaction(player, victimId, rewindTicks);
        }

        // 散弾は 1 発で何粒も当たるので、同じ弾が同じ相手に当てた分は 1 回と数え、どれかが頭なら頭と数える
        private void CountHeadshot(Watched player, int victimId, HitZone zone)
        {
            var head = zone == HitZone.Head;
            if (player.CountedSerial != player.ShotSerial)
            {
                player.CountedSerial = player.ShotSerial;
                player.Counted.Clear();
            }

            if (player.Counted.TryGetValue(victimId, out var countedHead))
            {
                if (!head || countedHead)
                {
                    return;
                }

                player.Heads++;
                player.Counted[victimId] = true;
            }
            else
            {
                player.Counted[victimId] = head;
                player.Hits++;
                if (head)
                {
                    player.Heads++;
                }
            }

            // 率は頭を数えたときだけ見る。散弾は粒の順が決まっておらず、体の粒で先に見ると同じ弾の頭の粒を待たずに低い率で出してしまうため
            if (!head || player.Hits < _stats.MinSamples)
            {
                return;
            }

            var ratio = (float)player.Heads / player.Hits;
            if (ratio > _stats.HeadshotRatio)
            {
                Report(player, ServerCheatMetric.HeadshotRatio, ratio, _stats.HeadshotRatio, player.Hits);
            }
        }

        // 反応は受け取った時刻でなく、撃った人が見ていた tick で測る。受け取った時刻で測ると遅延の分だけ遅く見え、速すぎる人を見逃すため。
        // 撃った tick はクライアントの申告 (ShotAccepted の shotTick) をそのまま使わず、当たり判定が丸めた巻き戻し幅 (rewindTicks) から出す。範囲外の申告で反応を偽られないため
        private void CountReaction(Watched player, int victimId, float rewindTicks)
        {
            if (!_sights.TryGetValue((player.PlayerId, victimId), out var sight) || !sight.Visible || sight.Reacted)
            {
                return;
            }

            sight.Reacted = true;
            var reactionMs = ((_tick - rewindTicks) - sight.VisibleSinceTick) * _tickSeconds * 1000f;

            // 見え始めより前に撃った弾は、見てから撃ったものではないので反応に数えない
            if (reactionMs < 0f)
            {
                return;
            }

            player.ReactionMsSum += reactionMs;
            player.Reactions++;
            if (player.Reactions < _stats.MinSamples)
            {
                return;
            }

            var mean = (float)(player.ReactionMsSum / player.Reactions);
            if (mean < _stats.MinReactionMs)
            {
                Report(player, ServerCheatMetric.ReactionMs, mean, _stats.MinReactionMs, player.Reactions);
            }
        }

        private void HandleLooked(int playerId, int tick, float yaw, float? pitch)
        {
            if (!TryWatch(playerId, out var player))
            {
                return;
            }

            // 落ちた入力の後では間の tick の回り方が分からないので、続いた 2 つの tick だけを比べる
            if (player.HasYaw && tick == player.LookTick + 1)
            {
                var degrees = MathF.Abs(DeltaRadians(player.Yaw, yaw)) * _radiansToDegrees;
                if (degrees > _stats.SnapDegreesPerTick)
                {
                    Report(player, ServerCheatMetric.SnapDegreesPerTick, degrees, _stats.SnapDegreesPerTick, null);
                }
            }

            player.HasYaw = true;
            player.LookTick = tick;
            player.Yaw = yaw;
        }

        // 出現し直すとサーバーが向きを決め直し、クライアントの向きもそこへ跳ぶので、その跳びを数えないよう前の向きを忘れる
        private void HandleTeleported(ServerBody body)
        {
            if (_players.TryGetValue(body.PlayerId, out var player))
            {
                player.HasYaw = false;
            }
        }

        // 抜けた人の番号は後から入る人に使い回されるので、見る側としても見られる側としても覚えを消す
        private void HandlePlayerLeft(ServerPlayer left)
        {
            var id = left.PlayerId;
            _players.Remove(id);
            _visibility.Forget(id);
            _gone.Clear();
            foreach (var key in _sights.Keys)
            {
                if (key.Viewer == id || key.Target == id)
                {
                    _gone.Add(key);
                }
            }

            foreach (var key in _gone)
            {
                _sights.Remove(key);
            }
        }

        private void HandleMapChanged(ServerMapData map)
        {
            _visibility.UseWorld(map.World);
            _sights.Clear();
            foreach (var player in _players.Values)
            {
                player.HasYaw = false;
            }
        }

        private void Observe(long tick)
        {
            _viewers.Clear();
            _targets.Clear();
            foreach (var body in _context.Bodies.All)
            {
                if (!body.HasSpawned || !_isHittable(body.PlayerId))
                {
                    continue;
                }

                _targets.Add(body);
                if (!BotRoster.IsBotId(body.PlayerId))
                {
                    _viewers.Add(body);
                }
            }

            foreach (var viewer in _viewers)
            {
                var player = Watch(viewer.PlayerId);
                var eye = viewer.EyePosition;
                foreach (var target in _targets)
                {
                    if (target.PlayerId == viewer.PlayerId || !_areHostile(viewer.PlayerId, target.PlayerId))
                    {
                        continue;
                    }

                    var key = (viewer.PlayerId, target.PlayerId);
                    if (!_sights.TryGetValue(key, out var sight))
                    {
                        sight = new Sight();
                        _sights.Add(key, sight);
                    }

                    sight.ObservedTick = tick;
                    var visible = _visibility.CanSeeAlone(viewer.PlayerId, eye, target.PlayerId, target.Center, target.EyePosition, _now);
                    Follow(player, sight, visible, tick, player.HasYaw && AimsAt(player.Yaw, eye, target));
                }
            }

            // 倒れた・味方になった相手の覚えは捨てる。出現し直した相手を見たときを、新しい見え始めとして数えるため
            _gone.Clear();
            foreach (var pair in _sights)
            {
                if (pair.Value.ObservedTick != tick)
                {
                    _gone.Add(pair.Key);
                }
            }

            foreach (var key in _gone)
            {
                _sights.Remove(key);
            }
        }

        private void Follow(Watched player, Sight sight, bool visible, long tick, bool aimed)
        {
            if (visible)
            {
                if (!sight.Visible)
                {
                    sight.Visible = true;
                    sight.VisibleSinceTick = tick;
                    sight.Reacted = false;
                }

                sight.TrackedSince = null;
                return;
            }

            sight.Visible = false;
            if (!aimed)
            {
                sight.TrackedSince = null;
                return;
            }

            sight.TrackedSince ??= _now;
            var seconds = (float)(_now - sight.TrackedSince.Value);
            if (seconds > _stats.WallTrackSeconds)
            {
                Report(player, ServerCheatMetric.WallTrackSeconds, seconds, _stats.WallTrackSeconds, null);
            }
        }

        // 入力は pitch を送らないので、横の向きだけで体の幅に向いているかを見る
        private static bool AimsAt(float yaw, Vector3 eye, ServerBody target)
        {
            var offset = target.Center - eye;
            var flat = new Vector2(offset.X, offset.Z);
            var distance = flat.Length();
            if (distance < _minAimDistance)
            {
                return false;
            }

            var forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            var along = Vector2.Dot(flat, forward);
            if (along <= 0f)
            {
                return false;
            }

            var side = MathF.Sqrt(MathF.Max(0f, (distance * distance) - (along * along)));
            return side <= target.Radius;
        }

        // 一周をまたぐ回り方 (359 度から 1 度) を 358 度と数えないよう、短い回り方の差にする
        private static float DeltaRadians(float from, float to)
        {
            return MathF.IEEERemainder(to - from, 2f * MathF.PI);
        }

        private void Roll(Watched player)
        {
            if (_now - player.WindowStart < _stats.WindowSeconds)
            {
                return;
            }

            player.WindowStart = _now;
            player.Hits = 0;
            player.Heads = 0;
            player.Reactions = 0;
            player.ReactionMsSum = 0d;
            player.Reported.Clear();
        }

        private bool TryWatch(int playerId, out Watched player)
        {
            if (BotRoster.IsBotId(playerId))
            {
                player = null;
                return false;
            }

            player = Watch(playerId);
            return true;
        }

        private Watched Watch(int playerId)
        {
            if (!_players.TryGetValue(playerId, out var player))
            {
                player = new Watched(playerId, _now);
                _players.Add(playerId, player);
            }

            return player;
        }

        private void Report(Watched player, ServerCheatMetric metric, float value, float threshold, int? samples)
        {
            if (!player.Reported.Add(metric))
            {
                return;
            }

            var productUserId = _context.Match.TryGetPlayer(player.PlayerId, out var joined) ? joined.ProductUserId : null;
            _context.Log.WriteLine(ServerCheatStatsLog.Suspicion(
                player.PlayerId, productUserId, metric, value, threshold, _stats.WindowSeconds, samples));
        }

        private sealed class Watched
        {
            public Watched(int playerId, double windowStart)
            {
                PlayerId = playerId;
                WindowStart = windowStart;
            }

            public int PlayerId { get; }

            public double WindowStart { get; set; }

            public HashSet<ServerCheatMetric> Reported { get; } = new HashSet<ServerCheatMetric>();

            public int Hits { get; set; }

            public int Heads { get; set; }

            public int Reactions { get; set; }

            public double ReactionMsSum { get; set; }

            public int ShotSerial { get; set; }

            public int CountedSerial { get; set; } = -1;

            public Dictionary<int, bool> Counted { get; } = new Dictionary<int, bool>();

            public bool HasYaw { get; set; }

            public int LookTick { get; set; }

            public float Yaw { get; set; }
        }

        private sealed class Sight
        {
            public long ObservedTick { get; set; }

            public bool Visible { get; set; }

            public long VisibleSinceTick { get; set; }

            public bool Reacted { get; set; }

            public double? TrackedSince { get; set; }
        }
    }
}
