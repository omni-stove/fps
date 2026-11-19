using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 種類ごとの値はデータ (server-data/tuning/deployables.json) から読み、置けるか (DeployPlacement)・タレットの狙い (TurretTargeting)・
    // 弾と時間 (TurretState)・囮の呼び声 (DecoyState) はオフラインと同じ Core で決める。人へのダメージは ServerCombat.DealDamage を通し、
    // 味方撃ち・出現直後の無敵・キルの数え方を撃った弾とそろえる。
    internal sealed partial class ServerDeployables : IServerModule, ISnapshotContributor
    {
        public const string CatalogFileName = "deployables.json";

        internal const string BeaconUnsupported = "a Beacon respawns players only offline; the dedicated server does not respawn at beacons yet";

        private const float _rejectWarningInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerCombat _combat;
        private readonly DeployCatalog _catalog;
        private readonly string _catalogPath;
        private readonly SortedDictionary<int, Placed> _placed = new SortedDictionary<int, Placed>();
        private readonly Dictionary<(int Owner, string TypeId), DeployRoster<Placed>> _rosters =
            new Dictionary<(int Owner, string TypeId), DeployRoster<Placed>>();
        private readonly Dictionary<string, WeaponSpec> _turretWeapons = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
        private readonly HashSet<string> _unusable = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Placed> _scratch = new List<Placed>();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private ServerRulesData _data;
        private int _nextId = 1;
        private float _now;
        private float _nextRejectWarningTime = float.NegativeInfinity;

        public ServerDeployables(MatchRulesModule rules, ServerCombat combat, DeployCatalog catalog = null, string catalogPath = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _catalog = catalog ?? DeployCatalog.Empty;
            _catalogPath = catalogPath;

            // 人の弾・爆発が置いた物に当たるよう、戦闘の当たり判定に置いた物を渡す (乗り物の ServerVehicles.Combat と同じつなぎ方)
            combat.Deployables = this;
        }

        public string Name => "deployables";

        public int Count => _placed.Count;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            _data = context.Rules ?? throw new InvalidOperationException("no match rules are loaded; start the server with " + ServerLaunch.RulesFlag);
            PrepareTypes();

            _match.PlayerLeft += HandlePlayerLeft;
            _match.MapChanged += HandleMapChanged;
            _rules.MatchStarted += HandleMatchStarted;
            _rules.RulesChanged += HandleRulesChanged;
            _log.WriteLine(ServerDeployableLog.Catalog(_catalog.Count, _catalog.Count - _unusable.Count, _catalogPath));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;

            // 撃った弾で他の置いた物が壊れて一覧が変わっても進め続けられるよう、写してから回す
            _scratch.Clear();
            _scratch.AddRange(_placed.Values);
            foreach (var placed in _scratch)
            {
                if (placed.Removed)
                {
                    continue;
                }

                switch (placed.Type.Role)
                {
                    case DeployRole.Turret:
                        TickTurret(placed, deltaSeconds);
                        break;
                    case DeployRole.Decoy:
                        TickDecoy(placed, deltaSeconds);
                        break;
                }
            }

            _scratch.Clear();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (message is DeployRequestMessage request && _match.TryGetPlayer(connection, out var player))
            {
                Place(player.PlayerId, request.TypeId, request.Position, request.Yaw);
            }
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerLeft -= HandlePlayerLeft;
            _match.MapChanged -= HandleMapChanged;
            _rules.MatchStarted -= HandleMatchStarted;
            _rules.RulesChanged -= HandleRulesChanged;
            _placed.Clear();
            _rosters.Clear();
        }

        // 置ける物の種類は武器の並び (ルールのファイルのロードアウト) で決まり、ルールを替えても変わらないので選び直さない
        private void HandleRulesChanged(ServerRulesData data)
        {
            _data = data;
        }

        // Place が全体の数を SnapshotMessage.MaxDeployables までに抑えるので、置いた物は全部載る
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(receiver);
            var list = snapshot.Deployables;
            list.Clear();
            foreach (var placed in _placed.Values)
            {
                if (list.Count == list.MaxCount)
                {
                    break;
                }

                ref var row = ref list.Add();
                row.DeployableId = placed.Id;
                row.Present = DeployableFields.All;
                row.Kind = KindOf(placed.Type.Role);
                row.TypeId = placed.Type.Id;
                row.Team = placed.Team;
                row.HasOwner = _match.TryGetPlayer(placed.OwnerId, out _);
                row.OwnerId = row.HasOwner ? placed.OwnerId : 0;
                row.Position = placed.Position;

                // 向きは 1 つしか送れないので、タレットは回る頭の向きを送る。台は回らないので見た目は変わらない
                row.Yaw = placed.Type.Role == DeployRole.Turret ? placed.HeadYaw : placed.Yaw;
                row.Health = placed.Health;
            }
        }

        public static bool TryLoadCatalog(IReadOnlyList<string> searchRoots, out DeployCatalog catalog, out string path, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);
            catalog = DeployCatalog.Empty;
            path = null;
            foreach (var root in searchRoots)
            {
                var candidate = Path.GetFullPath(
                    Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, CatalogFileName));
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    catalog = DeployCatalogJson.Parse(File.ReadAllText(candidate), candidate);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    catalog = DeployCatalog.Empty;
                    error = "cannot read deployable tuning: " + e.Message;
                    return false;
                }

                path = candidate;
                error = null;
                return true;
            }

            // 置ける物はゲームごとの中身なので、ファイルが無ければ何も置かせずに動かす
            error = null;
            return true;
        }

        internal ServerDeployRefusal Place(int playerId, string typeId, Vector3 position, float yaw)
        {
            if (_data.Rules?.Actions.Abilities != Abilities.On)
            {
                return Refuse(playerId, null, ServerDeployRefusal.RuleOff);
            }

            if (!_rules.CanFire(playerId) || !_bodies.TryGet(playerId, out var body) || !body.HasSpawned)
            {
                return Refuse(playerId, null, ServerDeployRefusal.CannotAct);
            }

            if (!_catalog.TryGet(typeId, out var type))
            {
                return Refuse(playerId, null, ServerDeployRefusal.UnknownType);
            }

            if (_unusable.Contains(type.Id))
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.Unusable);
            }

            if (!IsFinite(position) || !float.IsFinite(yaw))
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.NoGround);
            }

            // 持ち主の画面の位置はサーバーより遅れているので、届く距離には撃った弾の始点と同じだけのずれを許す
            var spec = type.Deploy;
            var offset = position - body.Position;
            var horizontal = MathF.Sqrt((offset.X * offset.X) + (offset.Z * offset.Z));
            if (horizontal > spec.MaxRange + _data.Settings.MaxShotOriginOffset)
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.OutOfReach);
            }

            // 真上の屋根や壁の向こうの床を送られても置けないよう、上下のずれに上限を置き、持ち主の体から置く地点まで壁が無いかも見る
            if (MathF.Abs(offset.Y) > MaxVerticalReach)
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.OutOfReach);
            }

            var roster = RosterOf(playerId, type);
            if (!roster.CanPlace)
            {
                if (CanWarn())
                {
                    _log.WriteLine(DeployLog.LimitReached(ServerDeployableLog.Owner(playerId), type.Id, roster.Count, roster.MaxActive));
                }

                return ServerDeployRefusal.LimitReached;
            }

            var facing = NormalizeYaw(yaw);
            var verdict = Survey(spec, position, facing, out var point);
            if (verdict != DeployVerdict.Ok)
            {
                return Refuse(playerId, type.Id, RefusalOf(verdict));
            }

            // 置く地点の物 (梁など) は足場の判定が Blocked で断るので、射線はその後に見る
            var sight = position + new Vector3(0f, SightLift, 0f) - body.Center;
            var sightDistance = sight.Length();
            if (sightDistance > 1e-4f
                && _context.World.Raycast(body.Center, sight / sightDistance, sightDistance, ServerHitboxes.RaycastLayers, out _))
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.OutOfReach);
            }

            // サーバーにある物は必ずスナップショットに載せるため、全員分の合計が送れる数に達したら置かせない。
            // 置き直しで一番古い物が消える分は数が増えないので、消える数を引いて比べる
            var evicting = Math.Max(0, roster.Count - roster.MaxActive + 1);
            if (_placed.Count - evicting >= SnapshotMessage.MaxDeployables)
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.LimitReached);
            }

            var placed = new Placed(NextId(), type, playerId, _rules.Session.GetTeam(playerId), point, facing, spec.MaxHealth);
            if (type.Role == DeployRole.Turret)
            {
                var weapon = _turretWeapons[type.Id];
                placed.Arm(new TurretState(type.Turret, weapon.FireInterval), weapon);
            }
            else if (type.Role == DeployRole.Decoy)
            {
                placed.Decoy = new DecoyState(type.Decoy);
            }

            foreach (var evicted in roster.Add(placed))
            {
                Remove(evicted, DeployRemoval.Replaced);
            }

            _placed.Add(placed.Id, placed);
            var owner = ServerDeployableLog.Owner(playerId);
            _log.WriteLine(DeployLog.Placed(owner, type.Id, placed.Id, point, roster.Count, roster.MaxActive));
            if (placed.Turret != null)
            {
                _log.WriteLine(TurretLog.Armed(
                    owner, type.Id, placed.Id, placed.Weapon.Id, type.Turret.Rounds, type.Turret.LifetimeSeconds, _data.Rules.FriendlyFire));
            }

            return ServerDeployRefusal.None;
        }

        // 戦闘の弾や爆発が置いた物に当たったときの口。持ち主と味方の弾は止めずに通す (オフラインの DeployPlacement.LetsThrough と同じ)
        internal bool Damage(int deployableId, int? attackerId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (!_placed.TryGetValue(deployableId, out var placed) || (attackerId.HasValue && LetsThrough(attackerId.Value, placed)))
            {
                return false;
            }

            placed.Health = Math.Max(0, placed.Health - amount);
            _log.WriteLine(DeployLog.Blocked(
                ServerDeployableLog.Owner(placed.OwnerId),
                placed.Type.Id,
                placed.Id,
                amount,
                placed.Health,
                attackerId.HasValue ? ServerDeployableLog.Owner(attackerId.Value) : null));
            if (placed.Health == 0)
            {
                Remove(placed, DeployRemoval.Destroyed);
            }

            return true;
        }

        // 戦闘の弾が置いた物で止まるかを見る口。撃った人の弾を通す物 (自分と味方の置いた物) は飛ばす
        internal bool Raycast(Vector3 origin, Vector3 unit, float maxDistance, int shooterId, out int deployableId, out float distance)
        {
            return Raycast(origin, unit, maxDistance, shooterId, null, out deployableId, out distance, out _);
        }

        internal bool Raycast(
            Vector3 origin, Vector3 unit, float maxDistance, int shooterId, out int deployableId, out float distance, out Vector3 normal)
        {
            return Raycast(origin, unit, maxDistance, shooterId, null, out deployableId, out distance, out normal);
        }

        // shooterId が null なら、誰の弾かで通す物を選ばず、どの置いた物でも止まる (爆風の遮り)
        private bool Raycast(
            Vector3 origin, Vector3 unit, float maxDistance, int? shooterId, int? skipId, out int deployableId, out float distance, out Vector3 normal)
        {
            deployableId = 0;
            distance = maxDistance;
            normal = -unit;
            var found = false;
            foreach (var placed in _placed.Values)
            {
                if (placed.Id == skipId || (shooterId.HasValue && LetsThrough(shooterId.Value, placed)))
                {
                    continue;
                }

                if (RayBox(origin, unit, distance, placed.Position, placed.Yaw, placed.Type.Deploy, out var hit, out var hitNormal)
                    && (!found || hit < distance))
                {
                    deployableId = placed.Id;
                    distance = hit;
                    normal = hitNormal;
                    found = true;
                }
            }

            return found;
        }

        internal bool TryGet(int deployableId, out DeployableSnapshot state)
        {
            state = default;
            if (!_placed.TryGetValue(deployableId, out var placed))
            {
                return false;
            }

            state.DeployableId = placed.Id;
            state.Kind = KindOf(placed.Type.Role);
            state.TypeId = placed.Type.Id;
            state.Team = placed.Team;
            state.HasOwner = true;
            state.OwnerId = placed.OwnerId;
            state.Position = placed.Position;
            state.Yaw = placed.Type.Role == DeployRole.Turret ? placed.HeadYaw : placed.Yaw;
            state.Health = placed.Health;
            state.Present = DeployableFields.All;
            return true;
        }

        private void PrepareTypes()
        {
            _turretWeapons.Clear();
            _unusable.Clear();
            foreach (var type in _catalog.All)
            {
                var problem = ProblemOf(type, out var weapon);
                if (problem != null)
                {
                    _unusable.Add(type.Id);
                    _log.WriteLine(ServerDeployableLog.Unusable(type.Id, problem));
                    continue;
                }

                if (weapon != null)
                {
                    _turretWeapons.Add(type.Id, weapon);
                }
            }
        }

        // タレットは撃った弾の届け方のうちヒットスキャンだけを撃つ。飛ぶ弾・ビーム・火炎は撃った人の連射の枠と弾の道筋を戦闘が持つため
        private string ProblemOf(DeployType type, out WeaponSpec weapon)
        {
            weapon = null;
            if (Encoding.UTF8.GetByteCount(type.Id) > MatchProtocol.MaxContentIdBytes)
            {
                return "the id is longer than " + MatchProtocol.MaxContentIdBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes and cannot be sent";
            }

            // 復活の場所はまだ専用サーバーの出現で選ばれず、スナップショットにも送る種類 (DeployableKind) が無い。置かせると使えない物が残るだけなので、
            // 置く前に断る
            if (type.Role == DeployRole.Beacon)
            {
                return BeaconUnsupported;
            }

            if (type.Role != DeployRole.Turret)
            {
                return null;
            }

            if (!TryFindWeapon(type.WeaponId, out weapon))
            {
                return "weapon " + type.WeaponId + " is not in the rules " + _data.Name;
            }

            if (weapon.Round.Delivery != RoundDelivery.Hitscan)
            {
                var delivery = weapon.Round.Delivery;
                weapon = null;
                return "weapon " + type.WeaponId + " delivers " + delivery + "; a turret fires only Hitscan rounds";
            }

            if (!(weapon.FireInterval > 0f))
            {
                weapon = null;
                return "weapon " + type.WeaponId + " has no fire interval";
            }

            return null;
        }

        private bool TryFindWeapon(string weaponId, out WeaponSpec weapon)
        {
            var file = _data.File;
            if (file.Catalog != null && file.Catalog.TryGet(weaponId, out weapon))
            {
                return true;
            }

            foreach (var candidate in file.LoadoutWeapons)
            {
                if (string.Equals(candidate.Id, weaponId, StringComparison.Ordinal))
                {
                    weapon = candidate;
                    return true;
                }
            }

            weapon = null;
            return false;
        }

        private DeployRoster<Placed> RosterOf(int playerId, DeployType type)
        {
            var key = (playerId, type.Id);
            if (!_rosters.TryGetValue(key, out var roster))
            {
                roster = new DeployRoster<Placed>(type.Deploy.MaxActive, type.Deploy.Overflow);
                _rosters.Add(key, roster);
            }

            return roster;
        }

        private void TickDecoy(Placed placed, float deltaSeconds)
        {
            var calls = placed.Decoy.Tick(deltaSeconds);
            for (var i = 0; i < calls; i++)
            {
                _log.WriteLine(DecoyLog.Called(ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id, placed.Position));
            }

            var over = placed.Decoy.Over;
            if (over.HasValue)
            {
                Remove(placed, over.Value);
            }
        }

        private void Remove(Placed placed, DeployRemoval reason)
        {
            if (placed.Removed)
            {
                return;
            }

            placed.Removed = true;
            _placed.Remove(placed.Id);
            if (_rosters.TryGetValue((placed.OwnerId, placed.Type.Id), out var roster))
            {
                roster.Remove(placed);
            }

            _log.WriteLine(DeployLog.Removed(ServerDeployableLog.Owner(placed.OwnerId), placed.Type.Id, placed.Id, reason));
        }

        // Tick が回している _scratch を、その最中に届いた知らせ (抜けた・試合が始まった) で消さないよう、別の一覧に集める
        private void RemoveAll(Func<Placed, bool> match, DeployRemoval reason)
        {
            var doomed = new List<Placed>();
            foreach (var placed in _placed.Values)
            {
                if (match(placed))
                {
                    doomed.Add(placed);
                }
            }

            foreach (var placed in doomed)
            {
                Remove(placed, reason);
            }
        }

        // 持ち主のいない物は撃った数を誰にも数えられないので、抜けた人の置いた物は一緒に消す
        private void HandlePlayerLeft(ServerPlayer player)
        {
            var owner = player.PlayerId;
            RemoveAll(placed => placed.OwnerId == owner, DeployRemoval.Discarded);
            var keys = new List<(int Owner, string TypeId)>();
            foreach (var key in _rosters.Keys)
            {
                if (key.Owner == owner)
                {
                    keys.Add(key);
                }
            }

            foreach (var key in keys)
            {
                _rosters.Remove(key);
            }
        }

        // 置いた位置は前のマップの当たりの上なので、新しいマップへ持ち込まない
        private void HandleMapChanged(ServerMapData map)
        {
            RemoveAll(_ => true, DeployRemoval.Discarded);
        }

        private void HandleMatchStarted()
        {
            RemoveAll(_ => true, DeployRemoval.Discarded);
        }

        private bool LetsThrough(int shooterId, Placed placed)
        {
            return DeployPlacement.LetsThrough(shooterId == placed.OwnerId, Side.FromTeam(placed.Team), SideOf(shooterId));
        }

        private Side? SideOf(int playerId)
        {
            var session = _rules.Session;
            return session != null && session.HasPlayer(playerId) ? Side.FromTeam(session.GetTeam(playerId)) : (Side?)null;
        }

        private int NextId()
        {
            var id = _nextId;
            _nextId = _nextId == int.MaxValue ? 1 : _nextId + 1;
            return id;
        }

        private ServerDeployRefusal Refuse(int playerId, string knownTypeId, ServerDeployRefusal reason)
        {
            if (CanWarn())
            {
                _log.WriteLine(ServerDeployableLog.Refused(playerId, knownTypeId, reason));
            }

            return reason;
        }

        private bool CanWarn()
        {
            if (_now < _nextRejectWarningTime)
            {
                return false;
            }

            _nextRejectWarningTime = _now + _rejectWarningInterval;
            return true;
        }

        private static ServerDeployRefusal RefusalOf(DeployVerdict verdict)
        {
            switch (verdict)
            {
                case DeployVerdict.NoGround:
                    return ServerDeployRefusal.NoGround;
                case DeployVerdict.TooSteep:
                    return ServerDeployRefusal.TooSteep;
                case DeployVerdict.Uneven:
                    return ServerDeployRefusal.Uneven;
                case DeployVerdict.Blocked:
                    return ServerDeployRefusal.Blocked;
                default:
                    return ServerDeployRefusal.None;
            }
        }

        private static DeployableKind KindOf(DeployRole role)
        {
            switch (role)
            {
                case DeployRole.Turret:
                    return DeployableKind.Turret;
                case DeployRole.Shield:
                    return DeployableKind.Shield;
                case DeployRole.Decoy:
                    return DeployableKind.Decoy;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined DeployRole");
            }
        }

        private sealed class Placed
        {
            public Placed(int id, DeployType type, int ownerId, Team team, Vector3 position, float yaw, int health)
            {
                Id = id;
                Type = type;
                OwnerId = ownerId;
                Team = team;
                Position = position;
                Yaw = yaw;
                HeadYaw = yaw;
                Health = health;
            }

            public int Id { get; }

            public DeployType Type { get; }

            public int OwnerId { get; }

            public Team Team { get; }

            public Vector3 Position { get; }

            public float Yaw { get; }

            public int Health { get; set; }

            public bool Removed { get; set; }

            public TurretState Turret { get; private set; }

            public WeaponSpec Weapon { get; private set; }

            public float HeadYaw { get; set; }

            public int? TargetId { get; set; }

            public int? HeldFor { get; set; }

            public float SearchIn { get; set; }

            public DecoyState Decoy { get; set; }

            public void Arm(TurretState turret, WeaponSpec weapon)
            {
                Turret = turret;
                Weapon = weapon;
            }
        }
    }
}
