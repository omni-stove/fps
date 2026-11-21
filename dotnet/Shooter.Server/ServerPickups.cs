using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 地面の拾い物 (マップの出現点と倒れた人が落とした物) と弾薬箱を置き、触れたら・拾う要求 (PickupRequestMessage) が届いたら、
    // オフラインと同じ判定 (PickupJudge) で拾う人に入れる。拾えるかはどのモードでも同じなので、目的 (IMatchObjectiveRules) には尋ねない。
    // 開けて中身を選ぶ箱 (ServerLoot) とは要求を分け、どちらの部品も相手の物の Id を断らずに無視する
    internal sealed partial class ServerPickups : IServerModule, ISnapshotContributor
    {
        public const string LogTag = ServerPickupsLog.LogTag;

        // 届く距離は半径で水平に測るが、上下の階の物を床越しに拾わせないよう、足元と物の高さの差にも上限を置く (ServerLoot と同じ)
        public const float MaxVerticalReach = 2f;

        // 射線の終点は物の載る床の面に当たらないよう、物の原点 (足元) より少し上にする
        public const float SightLift = 0.1f;

        // 持ち替えで手放した武器を、拾った物から拾った人の方へずらして置く距離。オフラインの Pickup.DroppedWeaponOffset と同じ
        public const float DroppedWeaponOffset = 0.6f;

        // 目的の物は 0 から、箱 (ServerLoot) は 1 << 24 から振るので、同じスナップショットの並びで重ならないよう、さらに離れた所から振る
        internal const int FirstItemId = 2 << 24;

        // 物の当たりと物の載る床を、物の真上のこの高さから探す
        private const float _ownProbeHeight = 1f;

        // 断った理由のログを出す最短の間隔 (秒)。押し続けてログを埋めないため
        private const float _refusalLogInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerCombat _combat;
        private readonly ServerInventories _inventories;
        private readonly List<Spot> _spots = new List<Spot>();
        private readonly List<Crate> _crates = new List<Crate>();
        private readonly List<Nearest> _nearest = new List<Nearest>();
        private readonly List<Spot> _scan = new List<Spot>();
        private readonly Dictionary<int, float> _nextRefusalLogTimes = new Dictionary<int, float>();
        private readonly HashSet<long> _touching = new HashSet<long>();
        private readonly HashSet<long> _stillTouching = new HashSet<long>();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private PickupDropSpec _drops = PickupDropSpec.Default;
        private ItemCatalog _items = ItemCatalog.Empty;
        private int _nextItemId = FirstItemId;
        private float _now;

        // 持ち物の無いサーバー (inventories が null) では、アイテムの拾い物は NoInventory で断り、武器は持ち物と揃えずに持たせる
        public ServerPickups(MatchRulesModule rules, ServerCombat combat, ServerInventories inventories = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _inventories = inventories;
        }

        public string Name => "pickups";

        // Start から Stop までの間。置く・拾う口はこの間だけ使える
        internal bool IsRunning => _match != null;

        internal int PickupCount => _spots.Count;

        internal int CrateCount => _crates.Count;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _drops = context.Rules?.PickupDrops ?? PickupDropSpec.Default;
            _items = context.Rules?.Items ?? ItemCatalog.Empty;
            _match.PlayerLeft += HandlePlayerLeft;
            _match.MapChanged += HandleMapChanged;
            _match.Restarted += HandleRestarted;
            _rules.MatchStarted += HandleMatchStarted;
            _rules.PlayerKilled += HandlePlayerKilled;
            _log.WriteLine(ServerPickupsLog.Started(_drops));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            if (_match == null)
            {
                return;
            }

            for (var i = _spots.Count - 1; i >= 0; i--)
            {
                var spot = _spots[i];
                if (spot.ExpiresAt.HasValue && _now >= spot.ExpiresAt.Value)
                {
                    _spots.RemoveAt(i);
                    _log.WriteLine(ServerPickupsLog.Expired(spot.ItemId));
                    continue;
                }

                if (spot.Respawn != null && spot.Respawn.Tick(deltaSeconds))
                {
                    spot.Spec = spot.FullSpec;
                    _log.WriteLine(ServerPickupsLog.Respawned(spot.ItemId));
                }
            }

            CollectTouched();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is PickupRequestMessage request) || _match == null || !_match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            Collect(player.PlayerId, request.ItemId);
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerLeft -= HandlePlayerLeft;
            _match.MapChanged -= HandleMapChanged;
            _match.Restarted -= HandleRestarted;
            _rules.MatchStarted -= HandleMatchStarted;
            _rules.PlayerKilled -= HandlePlayerKilled;
            Clear();
            _match = null;
        }

        // 受け取る人の近い順に、先に書いた物 (目的の物・箱) の後の空きに入るだけ載せる
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Vector3? near = null;
            if (receiver != null && _context != null && _context.Bodies.TryGet(receiver.PlayerId, out var body))
            {
                near = body.Position;
            }

            FillObjectiveItems(snapshot.ObjectiveItems, near);
        }

        // 並びを空にしないのは、目的と箱が先に書いた物の後に足すため。拾われて出直しを待つ物も Opened で載せ、
        // クライアントが同じ Id のまま隠して、出直したら同じ所に出せるようにする
        internal void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items, Vector3? near)
        {
            ArgumentNullException.ThrowIfNull(items);
            var room = items.MaxCount - items.Count;
            if (room <= 0 || (_spots.Count == 0 && _crates.Count == 0))
            {
                return;
            }

            _nearest.Clear();
            foreach (var spot in _spots)
            {
                _nearest.Add(new Nearest(spot.ItemId, spot.Kind, spot.Position, !spot.IsAvailable, spot.Radius, spot.OwnerId));
            }

            foreach (var crate in _crates)
            {
                _nearest.Add(new Nearest(crate.ItemId, ObjectiveItemKind.AmmoCrate, crate.Position, _now < crate.ReadyAt, crate.Radius, null));
            }

            if (_nearest.Count > room && near.HasValue)
            {
                var from = near.Value;
                _nearest.Sort((a, b) => HorizontalDistanceSquared(a.Position, from).CompareTo(HorizontalDistanceSquared(b.Position, from)));
            }

            var count = Math.Min(room, _nearest.Count);
            for (var i = 0; i < count; i++)
            {
                var shown = _nearest[i];
                ref var item = ref items.Add();
                item.ItemId = shown.ItemId;
                item.Present = ObjectiveItemFields.All;
                item.Kind = shown.Kind;
                item.Team = Team.Alpha;
                item.HasOwner = shown.OwnerId.HasValue;
                item.OwnerId = shown.OwnerId ?? 0;
                item.Position = shown.Position;
                item.State = ObjectiveItemState.Home;
                item.HasCarrier = false;
                item.CarrierId = 0;
                item.Opened = shown.Opened;
                item.Radius = shown.Radius;
            }

            _nearest.Clear();
        }

        // 拾える物の今の中身。無い Id・拾われて出直しを待つ物なら false
        internal bool TryGetAvailable(int itemId, out PickupSpec spec)
        {
            var spot = FindSpot(itemId);
            spec = spot != null && spot.IsAvailable ? spot.Spec : null;
            return spec != null;
        }

        internal bool IsCoolingDown(int itemId)
        {
            var crate = FindCrate(itemId);
            return crate != null && _now < crate.ReadyAt;
        }

        // 置いた順 (出現点、弾薬箱の順にマップのファイルの並び)。テストとログで物の Id を引くため
        internal IReadOnlyList<int> ItemIds()
        {
            var ids = new List<int>(_spots.Count + _crates.Count);
            foreach (var spot in _spots)
            {
                ids.Add(spot.ItemId);
            }

            foreach (var crate in _crates)
            {
                ids.Add(crate.ItemId);
            }

            return ids;
        }

        // Id を試合ごとに振り直さないのは、前の試合の物へ送った拾う要求を、同じ Id の新しい物への要求と取り違えないため
        private void Place()
        {
            Clear();
            var map = _context.Map;
            var pickups = map.Pickups;
            foreach (var point in pickups.Pickups)
            {
                var source = "map:" + point.Id;
                WeaponSpec weapon = null;
                if (point.Spec.Kind == PickupKind.Weapon)
                {
                    weapon = WeaponFor(point.Spec.WeaponId);
                    if (weapon == null)
                    {
                        _log.WriteLine(ServerPickupsLog.UnknownWeapon(source, point.Spec.WeaponId));
                        continue;
                    }
                }

                var spot = new Spot(
                    NextItemId(), point.Spec, point.Position, point.Radius, point.Touch, new PickupRespawn(point.RespawnSeconds), null, null, null, weapon);
                _spots.Add(spot);
                _log.WriteLine(ServerPickupsLog.Spawned(spot.ItemId, source, spot.Spec, spot.Position, spot.Touch));
            }

            foreach (var point in pickups.AmmoCrates)
            {
                var crate = new Crate(NextItemId(), point.Position, point.Radius, point.CooldownSeconds);
                _crates.Add(crate);
                _log.WriteLine(ServerPickupsLog.Crate(crate.ItemId, point.Id, crate.Position, crate.CooldownSeconds));
            }

            _log.WriteLine(ServerPickupsLog.Placed(map.Name, _spots.Count, _crates.Count));
        }

        private void Clear()
        {
            _spots.Clear();
            _crates.Clear();
            _touching.Clear();
            _nextRefusalLogTimes.Clear();
        }

        private void HandleMatchStarted()
        {
            Place();
        }

        // 同じマップで試合をやり直すときは MatchStarted が出ないので、ここでも置き直して前の試合の落とし物を消す
        private void HandleRestarted()
        {
            Place();
        }

        // 置いた位置は前のマップの上なので、新しいマップへ持ち込まない。新しいマップの物は次の試合の始まりで置く
        private void HandleMapChanged(ServerMapData map)
        {
            Clear();
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _nextRefusalLogTimes.Remove(player.PlayerId);
            _touching.RemoveWhere(key => (int)(key & 0xffffffffL) == player.PlayerId);
        }

        // ルールが替わればロードアウトも替わるので、置くたびに今のロードアウトから引く (ServerLoot と同じ)
        private WeaponSpec WeaponFor(string weaponId)
        {
            var loadout = _rules.Loadout;
            if (loadout == null || weaponId == null)
            {
                return null;
            }

            if (loadout.Catalog != null && loadout.Catalog.TryGet(weaponId, out var spec))
            {
                return spec;
            }

            foreach (var weapon in loadout.Weapons)
            {
                if (string.Equals(weapon.Id, weaponId, StringComparison.Ordinal))
                {
                    return weapon;
                }
            }

            return null;
        }

        private void RequireStarted()
        {
            if (_match == null)
            {
                throw new InvalidOperationException("the pickups module is not running; collect and drop between Start and Stop");
            }
        }

        private Spot FindSpot(int itemId)
        {
            foreach (var spot in _spots)
            {
                if (spot.ItemId == itemId)
                {
                    return spot;
                }
            }

            return null;
        }

        private Crate FindCrate(int itemId)
        {
            foreach (var crate in _crates)
            {
                if (crate.ItemId == itemId)
                {
                    return crate;
                }
            }

            return null;
        }

        private int NextItemId()
        {
            var id = _nextItemId;
            _nextItemId = _nextItemId == int.MaxValue ? FirstItemId : _nextItemId + 1;
            return id;
        }

        private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (dx * dx) + (dz * dz);
        }

        private sealed class Spot
        {
            public Spot(
                int itemId,
                PickupSpec spec,
                Vector3 position,
                float radius,
                bool touch,
                PickupRespawn respawn,
                float? expiresAt,
                int? ownerId,
                ServerHeldWeapon carried,
                WeaponSpec weapon)
            {
                ItemId = itemId;
                Spec = spec;
                FullSpec = spec;
                Kind = PickupItemKinds.ItemKindOf(spec.Kind);
                Position = position;
                Radius = radius;
                Touch = touch;
                Respawn = respawn;
                ExpiresAt = expiresAt;
                OwnerId = ownerId;
                Carried = carried;
                Weapon = carried?.Spec ?? weapon;
            }

            public int ItemId { get; }

            // アイテムが持ち物に入り切らなかったら、残りの数にして置いておく (オフラインの Pickup と同じ)
            public PickupSpec Spec { get; set; }

            // 出直したときに戻す中身
            public PickupSpec FullSpec { get; }

            public ObjectiveItemKind Kind { get; }

            public Vector3 Position { get; }

            public float Radius { get; }

            public bool Touch { get; }

            // マップの出現点だけが持つ。落とし物は拾われたら消える
            public PickupRespawn Respawn { get; }

            public float? ExpiresAt { get; }

            // 落とした人。マップの物は null
            public int? OwnerId { get; }

            // 誰かが落とした武器の弾数と付けた物。マップの武器は null (満タンで持たせる)
            public ServerHeldWeapon Carried { get; }

            // 武器の拾い物で持たせる武器。置くときに引いておき、ルールが替わっても拾う時に引き直して失敗しないようにする
            public WeaponSpec Weapon { get; }

            public bool IsAvailable => Respawn == null || Respawn.IsAvailable;
        }

        private sealed class Crate
        {
            public Crate(int itemId, Vector3 position, float radius, float cooldownSeconds)
            {
                ItemId = itemId;
                Position = position;
                Radius = radius;
                CooldownSeconds = cooldownSeconds;
            }

            public int ItemId { get; }

            public Vector3 Position { get; }

            public float Radius { get; }

            public float CooldownSeconds { get; }

            public float ReadyAt { get; set; }
        }

        private readonly struct Nearest
        {
            public Nearest(int itemId, ObjectiveItemKind kind, Vector3 position, bool opened, float radius, int? ownerId)
            {
                ItemId = itemId;
                Kind = kind;
                Position = position;
                Opened = opened;
                Radius = radius;
                OwnerId = ownerId;
            }

            public int ItemId { get; }

            public ObjectiveItemKind Kind { get; }

            public Vector3 Position { get; }

            public bool Opened { get; }

            public float Radius { get; }

            public int? OwnerId { get; }
        }
    }
}
