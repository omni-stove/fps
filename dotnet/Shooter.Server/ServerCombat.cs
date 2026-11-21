using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // ダメージはすべてルールのモジュールの MatchRulesModule.ApplyDamage を通すので、味方撃ち・出現直後の無敵・ダウン・キルの数え方と
    // キルフィード・JSON の kill の行はルールのモジュールが決めて出す。
    internal sealed partial class ServerCombat : IServerModule, ISnapshotContributor
    {
        public const string LogTag = "[Combat]";

        // 不正な要求の警告を出す最短の間隔 (秒)。連打でログを埋めないため
        private const float _rejectWarningInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerMovement _movement;
        private readonly Dictionary<int, Fighter> _fighters = new Dictionary<int, Fighter>();
        // 1 回の着弾・爆発で同じ建物の部品がいくつ壊れても、その建物の状態を送るのは 1 度だけにする
        private readonly SortedSet<int> _changedStructures = new SortedSet<int>();
        private readonly ServerSightBlockers _sightBlockers = new ServerSightBlockers();
        private readonly ServerImpact _impact;
        private readonly ServerProjectiles _projectiles;
        private readonly ServerBeams _beams;
        private readonly ServerSpray _spray;
        private readonly ServerStatusEffects _statusEffects;
        private readonly ServerLockOn _lockOn;
        private readonly ServerAreaEffects _areaEffects;
        private readonly ServerFlash _flash;
        private readonly ServerVehicles _vehicles;
        private readonly ServerCheatSignals _signals;
        // 座席の武器の弾と連射の間隔は、座る人が代わっても武器に残るので、人ではなく乗り物と武器の番号で持つ
        private readonly Dictionary<(int Vehicle, int Weapon), ServerPlayerWeapons> _mounts =
            new Dictionary<(int Vehicle, int Weapon), ServerPlayerWeapons>();
        // 座席の武器は ServerLoadout の人ごとの制限を通らないので、連射の間隔は武器ごとにここで持つ
        private readonly Dictionary<ServerPlayerWeapons, ShotBudget> _mountBudgets = new Dictionary<ServerPlayerWeapons, ShotBudget>();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private ServerRulesData _data;
        private ServerHitboxes _hitboxes;
        private ServerStructures _structures;
        private ServerShotTrace _shotTrace;
        private double _now;
        private float _nextRejectWarningTime = float.NegativeInfinity;

        public ServerCombat(
            MatchRulesModule rules, ServerMovement movement, ServerVehicles vehicles = null, ServerCheatSignals signals = null)
        {
            _vehicles = vehicles;
            _signals = signals;
            if (vehicles != null)
            {
                vehicles.Combat = this;
            }

            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _impact = new ServerImpact(this);
            _projectiles = new ServerProjectiles(this);
            _beams = new ServerBeams(this);
            _spray = new ServerSpray(this);
            _statusEffects = new ServerStatusEffects(this);
            _lockOn = new ServerLockOn(this);
            _areaEffects = new ServerAreaEffects(this);
            _flash = new ServerFlash(this);
        }

        public string Name => "combat";

        // 近接・ストリーク・設置物のように戦闘の外で数える仕組みが、倒したことを知る口。撃った弾・爆発・落下・溺れも含めて、キルを数えるたびに出す
        public event Action<KillFeedEntry> PlayerKilled;

        internal ServerCheatSignals CheatSignals => _signals;

        internal ServerHitboxes Hitboxes => _hitboxes;

        internal ServerStructures Structures => _structures;

        internal ServerSightBlockers SightBlockers => _sightBlockers;

        internal ServerContext Context => _context;

        internal ServerImpact Impact => _impact;

        internal ServerStatusEffects StatusEffects => _statusEffects;

        internal MatchRulesModule Rules => _rules;

        internal ZoneDamageMultipliers ZoneDamage => _data.Rules.ZoneDamage;

        // ServerMelee が自分を差し込む。ServerMelee は ServerCombat に頼って作られるので、逆向きに持たせずこの形で受ける
        internal Func<int, bool> IsMeleeSwinging { get; set; }

        internal void InterruptReload(int playerId, ReloadInterrupt by)
        {
            if (!_data.Rules.Actions.InterruptsReload(by) || !_fighters.TryGetValue(playerId, out var fighter))
            {
                return;
            }

            if (fighter.Weapons.CancelReload())
            {
                _log.WriteLine(ServerCombatLog.ReloadInterrupted(playerId, by));
            }
        }

        // 構えている間ずっと取り消すと、構えたまま送ったリロードがすぐ消えて弾を込められないので、押し始めた tick だけ取り消す。
        private void InterruptReloadOnAim(Fighter fighter)
        {
            var aiming = _movement.IsAiming(fighter.PlayerId);
            if (aiming && !fighter.WasAiming)
            {
                InterruptReload(fighter.PlayerId, ReloadInterrupt.Aim);
            }

            fighter.WasAiming = aiming;
        }

        private bool IsReloadHeldOff(Fighter fighter)
        {
            var actions = _data.Rules.Actions;
            return (actions.InterruptsReload(ReloadInterrupt.Grenade) && fighter.Grenades.IsThrowing(_now))
                || (actions.InterruptsReload(ReloadInterrupt.Melee) && IsMeleeSwinging != null && IsMeleeSwinging(fighter.PlayerId));
        }

        internal ServerAreaEffects AreaEffects => _areaEffects;

        internal ServerFlash Flash => _flash;

        internal TextWriter Log => _log;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            _data = context.Rules ?? throw new InvalidOperationException("no match rules are loaded; start the server with " + ServerLaunch.RulesFlag);
            UseMap(context.Map);
            var rules = _data.Rules;

            _match.PlayerJoined += HandlePlayerJoined;
            _match.MapChanged += HandleMapChanged;
            _match.PlayerLeft += HandlePlayerLeft;
            _bodies.Teleported += HandleTeleported;
            _rules.PlayerSpawned += HandlePlayerSpawned;
            _rules.MatchStarted += HandleMatchStarted;
            _rules.PlayerKilled += HandlePlayerKilled;
            _rules.WeaponsChanged += HandleWeaponsChanged;
            _rules.RulesChanged += HandleRulesChanged;
            _movement.Landed += HandleLanded;
            _movement.Drowned += HandleDrowned;

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
                InterruptReloadOnAim(fighter);
                fighter.Weapons.Tick(deltaSeconds);
                if (_bodies.TryGet(fighter.PlayerId, out var body))
                {
                    body.SetWeapons(fighter.Weapons.CurrentSpec, fighter.Weapons.Specs);
                }
            }

            foreach (var mount in _mounts.Values)
            {
                mount.Tick(deltaSeconds);
            }

            TickGrenades(deltaSeconds);
            _beams.Tick(deltaSeconds);
            _spray.Tick(deltaSeconds);
            _statusEffects.Tick(deltaSeconds);
            _lockOn.Tick(deltaSeconds);
            _areaEffects.Tick(deltaSeconds);
            _hitboxes.Record((int)Math.Min(tick, int.MaxValue));

            // 飛ぶ弾は巻き戻さずに今の位置へ当てるので、この tick の位置を残してから進める
            _projectiles.Tick(deltaSeconds);
        }

        public void OnConnected(ConnectionId connection)
        {
            // 途中から入った人にも、それまでに壊れた建物を見せる
            _structures.SendState(_context.Messenger, connection);
            _areaEffects.SendState(_context.Messenger, connection);
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
                    var weapons = ActiveWeapons(fighter);
                    if (action.Action == WeaponAction.ActiveReload)
                    {
                        weapons.TryActiveReload();
                    }
                    else if (action.Action == WeaponAction.Reload && !IsReloadHeldOff(fighter))
                    {
                        weapons.TryReload();
                    }
                    else if (action.Action == WeaponAction.SwitchAmmoType)
                    {
                        weapons.TrySwitchAmmoType();
                    }

                    break;
                case SwitchWeaponRequestMessage weaponSwitch:
                    fighter.Weapons.TrySwitch(weaponSwitch.WeaponId);
                    break;
                case ThrowGrenadeRequestMessage grenade:
                    ThrowGrenade(fighter, grenade.Direction, grenade.GrenadeTypeId);
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
            _match.MapChanged -= HandleMapChanged;
            _match.PlayerLeft -= HandlePlayerLeft;
            _bodies.Teleported -= HandleTeleported;
            _rules.PlayerSpawned -= HandlePlayerSpawned;
            _rules.MatchStarted -= HandleMatchStarted;
            _rules.PlayerKilled -= HandlePlayerKilled;
            _rules.WeaponsChanged -= HandleWeaponsChanged;
            _rules.RulesChanged -= HandleRulesChanged;
            _movement.Landed -= HandleLanded;
            _movement.Drowned -= HandleDrowned;
            _fighters.Clear();
            _grenades.Clear();
            _projectiles.Clear();
            _statusEffects.Clear();
        }

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
                    row.WeaponId = ActiveWeapons(fighter).CurrentSpec?.Id ?? "";
                    row.LastHitDirection = fighter.LastHitDirection;
                }
            }

            if (_fighters.TryGetValue(receiver.PlayerId, out var own))
            {
                // 座席の武器に座っている間は、持ち主の画面と射撃がその武器を今の武器として扱うよう、座席の武器を載せる
                var owner = snapshot.Owner;
                var active = ActiveWeapons(own);
                owner.Weapon = active.Sync();
                active.FillIds(owner.EquippedWeaponIds);
                own.Grenades.Fill(owner.Grenades);
            }

            ContributeGrenades(snapshot.Grenades);
            _projectiles.Contribute(snapshot.Projectiles);
        }

        private void HandlePlayerJoined(ServerPlayer player)
        {
            GetOrAdd(player.PlayerId);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _fighters.Remove(player.PlayerId);
            _hitboxes.Forget(player.PlayerId);
            _beams.Forget(player.PlayerId);
            _spray.Forget(player.PlayerId);
            _statusEffects.Forget(player.PlayerId);
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
            EquipChosen(fighter, playerId);
            RefillGrenades(fighter);
        }

        // Gun Game で段が上がった人は、出直しを待たずにその場で次の段の武器に持ち替える (オフラインの GunGameRunner と同じ)
        private void HandleWeaponsChanged(int playerId)
        {
            EquipChosen(GetOrAdd(playerId), playerId);
        }

        // 目的が配る武器を無しにする (Extraction で持ち込める物が無い) ときは、前の出撃の武器を残さず手ぶらにする
        private void EquipChosen(Fighter fighter, int playerId)
        {
            var specs = _rules.WeaponsFor(playerId);
            if (specs.Count == 0)
            {
                fighter.Weapons.Clear();
                return;
            }

            fighter.Weapons.Equip(specs, _data.File.SwitchSeconds, _data.Rules);
        }

        // ルールのモジュールは続けて全員を出現させるので、持ち替え (HandlePlayerSpawned) とグレネードの数はそこで新しいルールのものになる
        private void HandleRulesChanged(ServerRulesData data)
        {
            _data = data;
            _structures.Enabled = data.Rules.Destruction == Destruction.On;
            PrepareGrenades(data.Rules);
        }

        // 当たり判定は作るときに世界の当たりを受け取るので、作り直す。巻き戻しの記録は前のマップの位置なので一緒に捨てる
        private void UseMap(ServerMapData map)
        {
            _hitboxes = new ServerHitboxes(_bodies, map.World, null, _vehicles, _movement.Settings.Motor, _deployables);
            _shotTrace = new ServerShotTrace(_hitboxes);
            _structures = new ServerStructures(map, _log);
            _structures.Enabled = _data.Rules.Destruction == Destruction.On;
            _changedStructures.Clear();
        }

        // 飛んでいる物と燃えている範囲は前のマップの当たりで動いているので、新しいマップへ持ち込まない。座席の武器も乗り物ごと替わる
        private void HandleMapChanged(ServerMapData map)
        {
            UseMap(map);
            _mounts.Clear();
            _mountBudgets.Clear();
            ClearGrenades();
            _projectiles.Clear();
            _areaEffects.Clear();
            _log.WriteLine(LogTag + " map=" + map.Name + " destructibleStructures=" + Text(_structures.Count));
        }

        private void HandleMatchStarted()
        {
            _mounts.Clear();
            _mountBudgets.Clear();
            ClearGrenades();
            _projectiles.Clear();
            _statusEffects.Clear();
            _areaEffects.Clear();
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

        // 落下と同じく撃った人のいないダメージとしてルールに通し、倒れたら自滅として数えさせる
        private void HandleDrowned(ServerPlayer player, int tick, int drownings)
        {
            var damage = _movement.DrownDamage(drownings);
            if (damage <= 0)
            {
                return;
            }

            var outcome = _rules.ApplyDamage(null, player.PlayerId, damage);
            if (outcome != DamageOutcome.Ignored)
            {
                _log.WriteLine(ServerMovementLog.DrownDamage(player.PlayerId, tick, damage, outcome));
            }
        }

        // 近接・ストリーク・設置物が与えるダメージの口。撃った弾と同じく MatchRulesModule.ApplyDamage を通すので、味方撃ち・出現直後の無敵・
        // ダウン・キルの数え方とキルフィードは撃った弾と同じになる。攻撃者が抜けていれば、爆発と同じく攻撃者のいないダメージ (倒れたら自滅) にする
        internal DamageOutcome DealDamage(
            int? attackerId, int victimId, int amount, string weaponId, HitZone zone, Vector3? source, DamageKind kind = DamageKind.Other)
        {
            ArgumentNullException.ThrowIfNull(weaponId);
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (!IsHittable(victimId))
            {
                return DamageOutcome.Ignored;
            }

            var attacker = attackerId.HasValue && _match.TryGetPlayer(attackerId.Value, out _) ? attackerId : null;
            var outcome = _rules.ApplyDamage(attacker, victimId, amount, new KillShot(attacker ?? victimId, victimId, weaponId, zone));
            if (outcome == DamageOutcome.Ignored)
            {
                return outcome;
            }

            var reflected = attacker.HasValue && IsReflected(attacker.Value, victimId);
            var hurt = reflected ? attacker.Value : victimId;
            ReportStatusDamage(hurt, zone, kind, amount, outcome);
            if (source.HasValue && _fighters.TryGetValue(hurt, out var hurtFighter) && _bodies.TryGet(hurt, out var hurtBody))
            {
                hurtFighter.LastHitDirection = hurtBody.Position - source.Value;
            }

            if (!attacker.HasValue || attacker.Value == victimId)
            {
                SendDamageTaken(victimId, source, amount);
            }
            else if (reflected)
            {
                SendDamageTaken(attacker.Value, null, amount);
            }
            else
            {
                SendHitConfirm(attacker.Value, amount, zone, outcome);
                SendDamageTaken(victimId, source, amount);
            }

            _log.WriteLine(ServerCombatLog.Dealt(attacker, victimId, weaponId, amount, zone, outcome));
            return outcome;
        }

        private void HandlePlayerKilled(KillFeedEntry entry)
        {
            PlayerKilled?.Invoke(entry);
        }

        private ServerPlayerWeapons ActiveWeapons(Fighter fighter)
        {
            if (_vehicles == null
                || _vehicles.FiringOf(fighter.PlayerId, out var vehicle, out _, out var weapon) != VehicleSeatFiring.VehicleWeapon)
            {
                return fighter.Weapons;
            }

            return MountWeapons(vehicle, weapon);
        }

        private ServerPlayerWeapons MountWeapons(int vehicle, int weapon)
        {
            if (!_mounts.TryGetValue((vehicle, weapon), out var mount))
            {
                mount = new ServerPlayerWeapons(PlayerIds.None, _log, () => _now);
                mount.Equip(new[] { _vehicles.MapOf(vehicle).Weapons[weapon] }, _data.File.SwitchSeconds, _data.Rules);
                _mounts.Add((vehicle, weapon), mount);
            }

            return mount;
        }

        internal void DamageVehicle(in ServerShot shot, in ShotHit hit, int amount)
        {
            var damage = DamageCalculator.Scale(amount, hit.DamageMultiplier);
            var attacker = _match.TryGetPlayer(shot.ShooterId, out _) ? shot.ShooterId : (int?)null;
            var dealt = _vehicles.Damage(hit.VehicleIndex, damage, attacker, hit.VehiclePartId);
            if (attacker.HasValue && dealt > 0 && !_vehicleHitConfirmed)
            {
                _vehicleHitConfirmed = _collectingPelletHits;
                SendHitConfirm(shot.ShooterId, dealt, HitZone.Body, DamageOutcome.Damaged);
            }
        }

        internal ServerPlayerWeapons WeaponsOf(int playerId)
        {
            return GetOrAdd(playerId).Weapons;
        }

        private Fighter GetOrAdd(int playerId)
        {
            if (!_fighters.TryGetValue(playerId, out var fighter))
            {
                fighter = new Fighter(
                    new ServerPlayerWeapons(playerId, _log, () => _now),
                    new ServerGrenadeSupply(_data.Grenades, GrenadeThrowJitterSeconds),
                    playerId);
                _fighters.Add(playerId, fighter);
            }

            return fighter;
        }

        internal void Send(int playerId, IMessage message)
        {
            if (_match.TryGetPlayer(playerId, out var player))
            {
                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, message);
            }
        }

        internal void SendToAll(IMessage message, int? except = null)
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
                && session.Spec.Rules.FriendlyFire == FriendlyFire.Reflect
                && session.HasPlayer(attackerId) && session.HasPlayer(victimId)
                && !_rules.AreHostile(attackerId, victimId);
        }

        internal bool IsHittable(int playerId)
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

            public Vector3 LastHitDirection { get; set; }

            public bool WasAiming { get; set; }
        }
    }
}
