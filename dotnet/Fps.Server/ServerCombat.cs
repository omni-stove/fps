using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 戦闘。Unity の NetPlayer.Fire・ServerShotSequence・HitboxRewinder・NetTeamDeathmatch.Fire / .Explosion・ServerGrenadeSupply・NetGrenade・
    /// NetPlayerWeapon (サーバー側)・NetCombatFeed がしていたことを、Core の判定と <c>.fpsmap</c> の世界 (<see cref="IWorldCollision"/>) で行う。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ダメージはすべてルールのモジュールの <see cref="MatchRulesModule.ApplyDamage"/> を通すので、味方撃ち・出現直後の無敵・ダウン・キルの数え方と
    /// キルフィード・JSON の <c>kill</c> の行はルールのモジュールが決めて出す。ここは撃ってよいか・何に当たったか・いくつ減らすかを決め、
    /// 射撃・ヒット・被弾・爆発をクライアントへ送る。
    /// </para>
    /// <para>
    /// 位置は移動のモジュールが写した <see cref="ServerBodies"/> を読み、tick ごとに <see cref="ServerHitboxes"/> に残して巻き戻す。
    /// 回避・乗り越え・カバー・覗き込みと移動の速さは移動のモジュールの確定状態を読む。
    /// </para>
    /// </remarks>
    internal sealed partial class ServerCombat : IServerModule, ISnapshotContributor
    {
        public const string LogTag = "[Combat]";

        // 不正な要求の警告を出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _rejectWarningInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerMovement _movement;
        private readonly Dictionary<int, Fighter> _fighters = new Dictionary<int, Fighter>();
        private readonly List<int> _changedStructures = new List<int>();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private ServerRulesData _data;
        private ServerHitboxes _hitboxes;
        private ServerStructures _structures;
        private double _now;
        private float _nextRejectWarningTime = float.NegativeInfinity;

        public ServerCombat(MatchRulesModule rules, ServerMovement movement)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        public string Name => "combat";

        internal ServerHitboxes Hitboxes => _hitboxes;

        internal ServerStructures Structures => _structures;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            _data = context.Rules ?? throw new InvalidOperationException("no match rules are loaded; start the server with " + ServerLaunch.RulesFlag);
            _hitboxes = new ServerHitboxes(_bodies, context.World);
            _structures = new ServerStructures(context.Map, _log);
            var rules = _data.Rules;
            _structures.Enabled = rules.Destruction == Destruction.On;

            _match.PlayerJoined += HandlePlayerJoined;
            _match.PlayerLeft += HandlePlayerLeft;
            _bodies.Teleported += HandleTeleported;
            _rules.PlayerSpawned += HandlePlayerSpawned;
            _rules.MatchStarted += HandleMatchStarted;
            _movement.Landed += HandleLanded;

            _log.WriteLine(LogTag + " hitscan maxRewindTicks=" + Text(_hitboxes.MaxRewindTicks)
                + " maxShotOriginOffset=" + Text(_data.Settings.MaxShotOriginOffset)
                + " destructibleStructures=" + Text(_structures.Count) + " destruction=" + rules.Destruction);
            PrepareGrenades(rules);
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            foreach (var fighter in _fighters.Values)
            {
                fighter.Weapons.Tick(deltaSeconds);
            }

            TickGrenades(deltaSeconds);
            _hitboxes.Record((int)Math.Min(tick, int.MaxValue));
        }

        public void OnConnected(ConnectionId connection)
        {
            // 途中から入った人にも、それまでに壊れた建物を見せる
            _structures.SendState(_context.Messenger, connection);
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!_match.TryGetPlayer(connection, out var player) || !_fighters.TryGetValue(player.PlayerId, out var fighter))
            {
                return;
            }

            switch (message)
            {
                case FireRequestMessage fire:
                    Fire(fighter, fire);
                    break;
                case WeaponActionRequestMessage action:
                    if (action.Action == WeaponAction.ActiveReload)
                    {
                        fighter.Weapons.TryActiveReload();
                    }
                    else
                    {
                        fighter.Weapons.TryReload();
                    }

                    break;
                case SwitchWeaponRequestMessage weaponSwitch:
                    fighter.Weapons.TrySwitch(weaponSwitch.WeaponId);
                    break;
                case ThrowGrenadeRequestMessage grenade:
                    ThrowGrenade(fighter, grenade.Direction);
                    break;
            }
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerJoined -= HandlePlayerJoined;
            _match.PlayerLeft -= HandlePlayerLeft;
            _bodies.Teleported -= HandleTeleported;
            _rules.PlayerSpawned -= HandlePlayerSpawned;
            _rules.MatchStarted -= HandleMatchStarted;
            _movement.Landed -= HandleLanded;
            _fighters.Clear();
            _grenades.Clear();
        }

        /// <summary>
        /// 各プレイヤーの持っている武器と最後に受けた弾の向き、受け取る人の弾数・持たせている武器・グレネードの残り、飛んでいるグレネードを載せる。
        /// </summary>
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(receiver);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (_fighters.TryGetValue(row.PlayerId, out var fighter))
                {
                    row.WeaponId = fighter.Weapons.CurrentSpec?.Id ?? "";
                    row.LastHitDirection = fighter.LastHitDirection;
                }
            }

            if (_fighters.TryGetValue(receiver.PlayerId, out var own))
            {
                var owner = snapshot.Owner;
                owner.Weapon = own.Weapons.Sync();
                own.Weapons.FillIds(owner.EquippedWeaponIds);
                owner.Grenades = new GrenadePouchSync(own.Grenades.Enabled, own.Grenades.Remaining);
            }

            ContributeGrenades(snapshot.Grenades);
        }

        private void HandlePlayerJoined(ServerPlayer player)
        {
            GetOrAdd(player.PlayerId);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _fighters.Remove(player.PlayerId);
            _hitboxes.Forget(player.PlayerId);
        }

        // 置き直した人の前の位置と新しい位置の間を補間して当てないよう、それまでの記録を捨てる
        private void HandleTeleported(ServerBody body)
        {
            _hitboxes.Forget(body.PlayerId);
        }

        // 出現のたびに、選んだ並びの武器を満タンで持たせ直し、グレネードをルールの数に戻す
        private void HandlePlayerSpawned(int playerId)
        {
            var fighter = GetOrAdd(playerId);
            fighter.Weapons.Equip(_rules.WeaponsFor(playerId), _data.File.SwitchSeconds, _data.Rules);
            RefillGrenades(fighter);
        }

        private void HandleMatchStarted()
        {
            ClearGrenades();
            foreach (var fighter in _fighters.Values)
            {
                fighter.Weapons.Refill();
                RefillGrenades(fighter);
            }
        }

        // 落下ダメージは撃った人のいないダメージとしてルールに通し、倒れたら自滅として数えさせる (Unity の NetworkPlayerMotor.Landing と同じ)
        private void HandleLanded(ServerPlayer player, int tick, float fallSpeed)
        {
            var damage = _movement.FallDamage(fallSpeed, _data.Settings.MaxHealth);
            if (damage <= 0)
            {
                return;
            }

            var outcome = _rules.ApplyDamage(null, player.PlayerId, damage);
            if (outcome != DamageOutcome.Ignored)
            {
                _log.WriteLine("[Net] fall owner=" + Text(player.PlayerId) + " tick=" + Text(tick)
                    + " speed=" + fallSpeed.ToString("F2", CultureInfo.InvariantCulture) + " damage=" + Text(damage) + " outcome=" + outcome);
            }
        }

        private Fighter GetOrAdd(int playerId)
        {
            if (!_fighters.TryGetValue(playerId, out var fighter))
            {
                fighter = new Fighter(
                    new ServerPlayerWeapons(playerId, _log, () => _now),
                    new ServerGrenadeSupply(_data.Grenade.ThrowSeconds, GrenadeThrowJitterSeconds),
                    playerId);
                _fighters.Add(playerId, fighter);
            }

            return fighter;
        }

        private void Send(int playerId, IMessage message)
        {
            if (_match.TryGetPlayer(playerId, out var player))
            {
                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, message);
            }
        }

        private void SendToAll(IMessage message, int? except = null)
        {
            foreach (var player in _match.Players)
            {
                if (player.PlayerId != except)
                {
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, message);
                }
            }
        }

        // 跳ね返した味方撃ちなら、体力が減るのは撃った本人
        private bool IsReflected(int attackerId, int victimId)
        {
            var session = _rules.Session;
            return attackerId != victimId
                && _data.Rules.FriendlyFire == FriendlyFire.Reflect
                && session.HasPlayer(attackerId) && session.HasPlayer(victimId)
                && session.GetTeam(attackerId) == session.GetTeam(victimId);
        }

        private bool IsHittable(int playerId)
        {
            var session = _rules.Session;
            return session != null && session.HasPlayer(playerId) && session.IsAlive(playerId);
        }

        private void SendStructureChanges()
        {
            foreach (var structure in _changedStructures)
            {
                foreach (var player in _match.Players)
                {
                    _structures.Send(_context.Messenger, player.Connection, structure);
                }
            }

            _changedStructures.Clear();
        }

        private bool CanWarn()
        {
            var now = (float)_now;
            if (now < _nextRejectWarningTime)
            {
                return false;
            }

            _nextRejectWarningTime = now + _rejectWarningInterval;
            return true;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);

        private sealed class Fighter
        {
            public Fighter(ServerPlayerWeapons weapons, ServerGrenadeSupply grenades, int playerId)
            {
                Weapons = weapons;
                Grenades = grenades;
                PlayerId = playerId;
            }

            public int PlayerId { get; }

            public ServerPlayerWeapons Weapons { get; }

            public ServerGrenadeSupply Grenades { get; }

            public ServerShotSequence Shots { get; } = new ServerShotSequence();

            /// <summary>最後に受けた弾の進む向き。クライアントは倒れた体をこの向きへ倒す。</summary>
            public Vector3 LastHitDirection { get; set; }
        }
    }
}
