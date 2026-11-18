using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 入力の InputButtons.Melee を押した瞬間に振り始め、当たりの瞬間 (MeleeSpec.ImpactSeconds) に 1 度だけ MeleeJudge で相手を選んで
    // ServerCombat.DealDamage で与える。オフラインの MeleeController と同じく、振る者の中心から相手の体の表面までで届くかを測る。
    // Bot は入力のメッセージを送らないので振らない。
    internal sealed class ServerMelee : IServerModule
    {
        // 拒んだ警告を出す最短の間隔 (秒)。押し続けたり連打したりする入力でログを埋めないため
        private const float _refusalLogInterval = 1f;

        private readonly ServerCombat _combat;
        private readonly ServerMovement _movement;
        private readonly string _weaponId;
        private readonly Dictionary<int, Swinger> _swingers = new Dictionary<int, Swinger>();
        private readonly List<int> _candidateIds = new List<int>();
        private readonly List<Vector3> _candidatePoints = new List<Vector3>();
        private readonly List<int> _order = new List<int>();
        private MeleeSpec _spec;
        private ServerContext _context;
        private TextWriter _log;
        private double _now;
        private double _nextRefusalLogTime = double.NegativeInfinity;

        // weaponId を空にすると、オフラインの MeleeController.StrikeWeaponId と同じく持っている武器の Id で数える。
        // Gun Game の meleeWeaponIds のように近接を Id で見分けるルールに合わせるときは、ナイフの Id を渡す
        public ServerMelee(ServerCombat combat, ServerMovement movement, MeleeSpec spec, string weaponId = null)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _weaponId = weaponId ?? string.Empty;
        }

        public string Name => "melee";

        // 振っている最中に替えても、その振りは始めたときの値のまま終える (MeleeSwing.Spec と同じ)
        public MeleeSpec Spec
        {
            get => _spec;
            set
            {
                _spec = value ?? throw new ArgumentNullException(nameof(value));
                foreach (var swinger in _swingers.Values)
                {
                    swinger.Swing.Spec = value;
                }
            }
        }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            context.Match.PlayerLeft += HandlePlayerLeft;
            context.Match.MapChanged += HandleMapChanged;
            _combat.Rules.PlayerSpawned += HandlePlayerSpawned;
            _combat.Rules.MatchStarted += HandleMatchStarted;
            _log.WriteLine(ServerMeleeLog.Started(_spec, _weaponId, _combat.Rules.Rules?.Actions.Melee ?? Melee.Off));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            foreach (var swinger in _swingers.Values)
            {
                var swing = swinger.Swing;
                if (swing.IsSwinging && !IsStanding(swinger.PlayerId))
                {
                    swing.Cancel();
                    continue;
                }

                var spec = swing.CurrentSpec;
                if (swing.Tick(deltaSeconds))
                {
                    Strike(swinger.PlayerId, spec);
                }
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is InputMessage input) || input.Count == 0 || !_context.Match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            var swinger = GetOrAdd(player.PlayerId);
            for (var i = 0; i < input.Count; i++)
            {
                var frame = input[i];

                // 落ちてもよい届け方なので、同じ tick が何度も届く。押した瞬間を 2 度数えないよう、見た tick より古いものは捨てる
                if (frame.Tick <= swinger.LastTick)
                {
                    continue;
                }

                var held = frame.Has(InputButtons.Melee);
                var pressed = held && !swinger.Held;
                swinger.LastTick = frame.Tick;
                swinger.Held = held;
                if (pressed)
                {
                    TrySwing(swinger);
                }
            }
        }

        public void Stop()
        {
            if (_context == null)
            {
                return;
            }

            _context.Match.PlayerLeft -= HandlePlayerLeft;
            _context.Match.MapChanged -= HandleMapChanged;
            _combat.Rules.PlayerSpawned -= HandlePlayerSpawned;
            _combat.Rules.MatchStarted -= HandleMatchStarted;
            _swingers.Clear();
        }

        internal bool IsSwinging(int playerId)
        {
            return _swingers.TryGetValue(playerId, out var swinger) && swinger.Swing.IsSwinging;
        }

        internal float CooldownRemaining(int playerId)
        {
            return _swingers.TryGetValue(playerId, out var swinger) ? swinger.Swing.CooldownRemaining : 0f;
        }

        internal MeleeRefusal Check(int playerId)
        {
            if (_combat.Rules.Rules?.Actions.Melee != Melee.On)
            {
                return MeleeRefusal.Off;
            }

            if (!IsStanding(playerId)
                || _movement.IsRiding(playerId)
                || !_context.Bodies.TryGet(playerId, out var body)
                || !body.HasSpawned
                || body.IsClimbing)
            {
                return MeleeRefusal.Busy;
            }

            return !_swingers.TryGetValue(playerId, out var swinger) || swinger.Swing.CanStart ? MeleeRefusal.None : MeleeRefusal.Cooldown;
        }

        private void TrySwing(Swinger swinger)
        {
            var refusal = Check(swinger.PlayerId);
            if (refusal != MeleeRefusal.None)
            {
                if (_now >= _nextRefusalLogTime)
                {
                    _nextRefusalLogTime = _now + _refusalLogInterval;
                    _log.WriteLine(MeleeLog.Refused(Text(swinger.PlayerId), refusal));
                }

                return;
            }

            swinger.Swing.TryStart();
            _log.WriteLine(MeleeLog.Swing(Text(swinger.PlayerId)));
        }

        private void Strike(int attackerId, MeleeSpec spec)
        {
            if (!_context.Bodies.TryGet(attackerId, out var attacker))
            {
                return;
            }

            var origin = attacker.Center;
            var forward = new Vector3(MathF.Sin(attacker.Yaw), 0f, MathF.Cos(attacker.Yaw));
            var target = FindTarget(attackerId, origin, forward, spec, out var point);
            if (target < 0)
            {
                _log.WriteLine(MeleeLog.Miss(Text(attackerId)));
                return;
            }

            var distance = Vector3.Distance(origin, point);
            _log.WriteLine(MeleeLog.Hit(Text(attackerId), Text(target), spec.Damage, distance));
            _combat.DealDamage(attackerId, target, spec.Damage, StrikeWeaponId(attacker), HitZone.Body, attacker.Position);
        }

        // 並びは Id の小さい順にそろえ、同じ距離の相手を毎回同じに選ぶ (MeleeStrike.TryFind と同じ)。壁越しの相手は外す
        private int FindTarget(int attackerId, Vector3 origin, Vector3 forward, MeleeSpec spec, out Vector3 point)
        {
            _order.Clear();
            foreach (var body in _context.Bodies.All)
            {
                if (body.PlayerId != attackerId && body.HasSpawned && _combat.IsHittable(body.PlayerId))
                {
                    _order.Add(body.PlayerId);
                }
            }

            _order.Sort();
            _candidateIds.Clear();
            _candidatePoints.Clear();
            foreach (var playerId in _order)
            {
                _context.Bodies.TryGet(playerId, out var body);
                var closest = ClosestPointOnBody(body, origin);
                if (!IsBlocked(origin, closest))
                {
                    _candidateIds.Add(playerId);
                    _candidatePoints.Add(closest);
                }
            }

            var best = MeleeJudge.SelectNearest(origin, forward, spec, _candidatePoints);
            if (best < 0)
            {
                point = default;
                return -1;
            }

            point = _candidatePoints[best];
            return _candidateIds[best];
        }

        // 立ち姿・しゃがみ・伏せで変わるカプセルの表面のうち、振る者の中心にいちばん近い点。中心が体の中なら中心そのもの
        internal static Vector3 ClosestPointOnBody(ServerBody body, Vector3 origin)
        {
            body.Capsule(out var bottom, out var top);
            var axis = top - bottom;
            var lengthSquared = axis.LengthSquared();
            var t = lengthSquared > 1e-12f ? Math.Clamp(Vector3.Dot(origin - bottom, axis) / lengthSquared, 0f, 1f) : 0f;
            var onAxis = bottom + (axis * t);
            var offset = origin - onAxis;
            var length = offset.Length();
            return length <= body.Radius ? origin : onAxis + (offset * (body.Radius / length));
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f && _context.World.Raycast(from, offset / length, length, ServerHitboxes.RaycastLayers, out _);
        }

        private string StrikeWeaponId(ServerBody attacker)
        {
            return _weaponId.Length > 0 ? _weaponId : attacker.HeldWeapon?.Id ?? string.Empty;
        }

        private bool IsStanding(int playerId)
        {
            var session = _combat.Rules.Session;
            return session != null && session.HasPlayer(playerId) && session.IsAlive(playerId) && !session.IsDowned(playerId);
        }

        private Swinger GetOrAdd(int playerId)
        {
            if (!_swingers.TryGetValue(playerId, out var swinger))
            {
                swinger = new Swinger(playerId, new MeleeSwing(_spec));
                _swingers.Add(playerId, swinger);
            }

            return swinger;
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _swingers.Remove(player.PlayerId);
        }

        // 出現し直した人は振りかけの振りも待ち時間も持ち越さない (オフラインの MeleeController が出現で止めるのと同じ)
        private void HandlePlayerSpawned(int playerId)
        {
            if (_swingers.TryGetValue(playerId, out var swinger))
            {
                swinger.Swing.Reset();
            }
        }

        private void HandleMatchStarted()
        {
            foreach (var swinger in _swingers.Values)
            {
                swinger.Swing.Reset();
            }
        }

        private void HandleMapChanged(ServerMapData map)
        {
            HandleMatchStarted();
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private sealed class Swinger
        {
            public Swinger(int playerId, MeleeSwing swing)
            {
                PlayerId = playerId;
                Swing = swing;
            }

            public int PlayerId { get; }

            public MeleeSwing Swing { get; }

            public int LastTick { get; set; } = -1;

            public bool Held { get; set; }
        }
    }
}
