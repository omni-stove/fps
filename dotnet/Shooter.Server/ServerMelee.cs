using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 入力の InputButtons.Melee を押した瞬間に振り始め、当たりの瞬間 (MeleeSpec.ImpactSeconds) に 1 度だけ MeleeJudge で相手を選んで
    // ServerCombat.DealDamage で与える。オフラインの MeleeController と同じく、振る者の中心から相手の体の表面までで届くかを測る。
    // Bot は入力のメッセージを送らないので振らない。
    internal sealed partial class ServerMelee : IServerModule
    {
        private readonly ServerCombat _combat;
        private readonly ServerMovement _movement;
        private string _weaponId;
        private readonly Dictionary<int, Swinger> _swingers = new Dictionary<int, Swinger>();
        private MeleeSpec _spec;
        private ServerContext _context;
        private TextWriter _log;
        private double _now;

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
            _combat.IsMeleeSwinging = IsSwinging;
            context.Match.PlayerLeft += HandlePlayerLeft;
            context.Match.MapChanged += HandleMapChanged;
            _combat.Rules.PlayerSpawned += HandlePlayerSpawned;
            _combat.Rules.MatchStarted += HandleMatchStarted;
            _combat.Rules.RulesChanged += HandleRulesChanged;
            _log.WriteLine(ServerMeleeLog.Started(_spec, _weaponId, _combat.Rules.Rules?.Actions.Melee ?? Melee.Off));
        }

        // 起動で渡された Id は起動のルールに合わせたものなので、ルールが替わったら ServerModules と同じ決め方で選び直す
        private void HandleRulesChanged(ServerRulesData data)
        {
            _weaponId = ServerModules.MeleeWeaponIdFor(data) ?? string.Empty;
            _log.WriteLine(ServerMeleeLog.Started(_spec, _weaponId, data.Rules.Actions.Melee));
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
            _combat.Rules.RulesChanged -= HandleRulesChanged;
            _combat.IsMeleeSwinging = null;
            _swingers.Clear();
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
