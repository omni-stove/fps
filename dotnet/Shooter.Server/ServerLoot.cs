using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 箱 (地点ファイルの箱・物資投下・倒れた人の持ち物) を置き、開ける要求 (InteractRequestMessage) を受けて、開けた人の武器と持ち物に中身を入れる。
    // 中身は置くときに決めるので、同じ種なら同じ試合の箱は同じ中身になる。開けてよいかの最後の判断は目的 (IMatchObjectiveRules.Interact) に任せる
    internal sealed class ServerLoot : IServerModule, ISnapshotContributor
    {
        public const string LogTag = ServerLootLog.LogTag;

        // 物資投下と倒れた人の箱の届く距離。オフラインの LootContainer.DefaultReach と同じ
        public const float DefaultDropReach = 1.6f;

        // 目的の物 (旗・爆弾・台車) は 0 から振るので、同じスナップショットの並びで重ならないよう離れた所から振る
        internal const int FirstItemId = 1 << 24;

        // 断った理由のログを出す最短の間隔 (秒)。押し続けてログを埋めないため
        private const float _refusalLogInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerCombat _combat;
        private readonly ServerInventories _inventories;
        private readonly int _seed;
        private readonly float _dropReach;
        private readonly List<Box> _boxes = new List<Box>();
        private readonly Dictionary<int, float> _nextRefusalLogTimes = new Dictionary<int, float>();
        private readonly List<Box> _nearest = new List<Box>();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private LootSpec _loot = LootSpec.Default;
        private ItemCatalog _items = ItemCatalog.Empty;
        private Random _random;
        private int _nextItemId = FirstItemId;
        private float _now;

        // 種を渡さなければ起動の時刻から決め、ログに出す。出た種を渡せば同じ中身で試合をやり直せる
        public ServerLoot(MatchRulesModule rules, ServerCombat combat, ServerInventories inventories, int? seed = null, float dropReach = DefaultDropReach)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            if (!(dropReach > 0f) || float.IsInfinity(dropReach))
            {
                throw new ArgumentOutOfRangeException(nameof(dropReach), dropReach, "dropReach must be positive and finite");
            }

            _seed = seed ?? Environment.TickCount;
            _dropReach = dropReach;
        }

        public string Name => "loot";

        public int Seed => _seed;

        // Start から Stop までの間。箱を置く・開ける・持ち物を数える口はこの間だけ使える
        internal bool IsRunning => _match != null;

        // 箱を開けた人と、その人に入った物 (武器は持ち物の表の武器の物の Id で 1 つ) を、入れた後に知らせる。
        // 入り切らずに失った分は含めないので、持ち帰る物を数える目的 (Extraction) は入った物だけを数えられる
        internal event Action<int, IReadOnlyList<ItemCount>> Given;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _loot = context.Rules?.Loot ?? LootSpec.Default;
            _items = context.Rules?.Items ?? ItemCatalog.Empty;
            _random = new Random(_seed);
            _match.PlayerLeft += HandlePlayerLeft;
            _rules.MatchStarted += HandleMatchStarted;
            _log.WriteLine(ServerLootLog.Started(_seed, _loot.Tables.Count, _dropReach));
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
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is InteractRequestMessage request) || !_match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            Open(player.PlayerId, request.ItemId);
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerLeft -= HandlePlayerLeft;
            _rules.MatchStarted -= HandleMatchStarted;
            _boxes.Clear();
            _nextRefusalLogTimes.Clear();
            _match = null;
        }

        // 受け取る人の近い順に、目的の物の後の空きに入るだけ載せる。並びの上限 (SnapshotMessage.MaxObjectiveItems) より箱が多いマップでも、近くの箱は見えるようにする
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

        // 並びを空にしないのは、目的 (MatchRulesModule) が先に書いた物の後に足すため
        internal void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items, Vector3? near)
        {
            ArgumentNullException.ThrowIfNull(items);
            var room = items.MaxCount - items.Count;
            if (room <= 0 || _boxes.Count == 0)
            {
                return;
            }

            _nearest.Clear();
            _nearest.AddRange(_boxes);
            if (_nearest.Count > room && near.HasValue)
            {
                var from = near.Value;
                _nearest.Sort((a, b) => HorizontalDistanceSquared(a.Position, from).CompareTo(HorizontalDistanceSquared(b.Position, from)));
            }

            var count = Math.Min(room, _nearest.Count);
            for (var i = 0; i < count; i++)
            {
                var box = _nearest[i];
                ref var item = ref items.Add();
                item.ItemId = box.ItemId;
                item.Present = ObjectiveItemFields.All;
                item.Kind = box.Kind;
                item.Team = Team.Alpha;
                item.HasOwner = box.OwnerId.HasValue;
                item.OwnerId = box.OwnerId ?? 0;
                item.Position = box.Position;
                item.State = ObjectiveItemState.Home;
                item.HasCarrier = false;
                item.CarrierId = 0;
                item.Opened = box.Opened;
                item.Radius = box.Reach;
            }

            _nearest.Clear();
        }

        // 物資投下の箱。表に無い Id なら空の箱を置く (オフラインの SupplyDropper と同じ)。返すのはスナップショットの物の Id
        // reach を渡さなければコンストラクタの届く距離。物資投下の設定の値はここから渡す
        internal int SpawnCrate(Vector3 position, string tableId, float? reach = null)
        {
            ArgumentNullException.ThrowIfNull(tableId);
            if (reach.HasValue && (!(reach.Value > 0f) || float.IsInfinity(reach.Value)))
            {
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "reach must be positive and finite");
            }

            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            RequireStarted();

            var box = new Box(NextItemId(), ObjectiveItemKind.SupplyCrate, position, reach ?? _dropReach, null);
            Fill(box, tableId, "crate");
            _boxes.Add(box);
            _log.WriteLine(ServerLootLog.Box(box.ItemId, box.Kind, "crate:" + tableId, position, box.Weapons.Count, box.Items.Count));
            return box.ItemId;
        }

        // 倒れた人の武器と持ち物を取り出して、その人の居た所に箱を置く。体の無い人 (抜けた人) なら置かずに null
        internal int? SpawnDeathBox(int playerId)
        {
            RequireStarted();
            if (!_context.Bodies.TryGet(playerId, out var body))
            {
                _log.WriteLine(ServerLootLog.NoDeathBox(playerId));
                return null;
            }

            var box = new Box(NextItemId(), ObjectiveItemKind.DeathBox, body.Position, _dropReach, playerId);
            TakeAll(playerId, box.Weapons, box.Items);
            _boxes.Add(box);
            _log.WriteLine(ServerLootLog.Box(box.ItemId, box.Kind, ServerLootLog.PlayerSource(playerId), box.Position, box.Weapons.Count, box.Items.Count));
            return box.ItemId;
        }

        // 今持っている武器 (持ち物の表の武器の物の Id で 1 つずつ)・付けた物・持ち物の数。取り出さずに数えるだけ。
        // 表に無い武器は持ち物庫に入れられないので数えない
        internal IReadOnlyList<ItemCount> HeldItems(int playerId)
        {
            RequireStarted();
            var held = new List<ItemCount>();
            var weapons = _combat.WeaponsOf(playerId);
            foreach (var spec in weapons.Specs)
            {
                if (_items.TryFindWeapon(spec.Id, out var weaponItem))
                {
                    Merge(held, weaponItem.Id, 1);
                }

                foreach (var attachment in weapons.AttachmentsOf(spec.Id))
                {
                    Merge(held, attachment, 1);
                }
            }

            var inventory = _inventories.InventoryOf(playerId);
            if (inventory != null)
            {
                foreach (var stack in inventory.Stacks)
                {
                    if (inventory.WeaponIdOf(stack.Handle) == null)
                    {
                        Merge(held, stack.ItemId, stack.Count);
                    }
                }
            }

            return held;
        }

        // 脱出した人のように、持ち物を場から外す人の武器と持ち物を取り出して捨てる。外した数をログに出す
        internal void Stow(int playerId)
        {
            RequireStarted();
            var weapons = new List<WeaponSpec>();
            var items = new List<ItemCount>();
            TakeAll(playerId, weapons, items);
            var count = 0;
            foreach (var item in items)
            {
                count += item.Count;
            }

            _log.WriteLine(ServerLootLog.Stowed(playerId, weapons.Count, count));
        }

        // まだ開いていない箱のうち、水平に最も近いもの。無ければ false
        internal bool TryFindClosedBox(Vector3 from, out int itemId, out Vector3 position)
        {
            Box nearest = null;
            var best = float.PositiveInfinity;
            foreach (var box in _boxes)
            {
                if (box.Opened)
                {
                    continue;
                }

                var distance = HorizontalDistanceSquared(box.Position, from);
                if (distance < best)
                {
                    best = distance;
                    nearest = box;
                }
            }

            itemId = nearest?.ItemId ?? 0;
            position = nearest?.Position ?? Vector3.Zero;
            return nearest != null;
        }

        internal bool IsInReach(int itemId, int playerId)
        {
            var box = Find(itemId);
            return box != null && IsInReach(box, playerId);
        }

        internal bool TryGetContents(int itemId, out IReadOnlyList<WeaponSpec> weapons, out IReadOnlyList<ItemCount> items)
        {
            var box = Find(itemId);
            weapons = box?.Weapons;
            items = box?.Items;
            return box != null;
        }

        internal bool IsOpened(int itemId)
        {
            return Find(itemId)?.Opened ?? false;
        }

        // Bot の行き先 (BR の BattleRoyaleBotPlanner) を選ぶために、まだ開けていない箱を置いた順に足す。並びを空にしないのは、呼ぶ側が使い回すため
        internal void CollectUnopened(List<ServerLootBox> boxes)
        {
            ArgumentNullException.ThrowIfNull(boxes);
            foreach (var box in _boxes)
            {
                if (!box.Opened)
                {
                    boxes.Add(new ServerLootBox(box.ItemId, box.Kind, box.Position, box.Reach, box.Weapons.Count > 0));
                }
            }
        }

        // 断る理由は安いものから確かめ、目的に尋ねるのは最後にする。目的が受けたことを数える (開けた数など) なら、その後で断って数え違えさせないため
        internal InteractVerdict Open(int playerId, int itemId)
        {
            RequireStarted();
            var box = Find(itemId);
            var verdict = box == null ? InteractVerdict.Refused(InteractRefusal.UnknownItem)
                : !_rules.CanFire(playerId) ? InteractVerdict.Refused(InteractRefusal.CannotAct)
                : box.Opened ? InteractVerdict.Refused(InteractRefusal.AlreadyOpened)
                : !IsInReach(box, playerId) ? InteractVerdict.Refused(InteractRefusal.OutOfReach)
                : _rules.Objective?.Interact(playerId, itemId) ?? InteractVerdict.Refused(InteractRefusal.NotInteractable);
            if (!verdict.IsAccepted)
            {
                RefuseLog(itemId, playerId, verdict.Refusal);
                return verdict;
            }

            box.Opened = true;
            Give(box, playerId);
            return verdict;
        }

        private void Give(Box box, int playerId)
        {
            var weapons = _combat.WeaponsOf(playerId);
            var loadout = _rules.Loadout;
            var weaponsGiven = 0;
            var given = new List<ItemCount>();
            foreach (var spec in box.Weapons)
            {
                if (loadout != null && weapons.Give(spec, loadout.SwitchSeconds, _rules.Rules))
                {
                    weaponsGiven++;
                    if (_items.TryFindWeapon(spec.Id, out var weaponItem))
                    {
                        Merge(given, weaponItem.Id, 1);
                    }
                }
            }

            // 持ち物の武器の写しを先に置いてから、残りのマスに持ち物を入れる
            if (weaponsGiven > 0)
            {
                foreach (var weaponId in _inventories.HoldWeapons(playerId))
                {
                    _log.WriteLine(ServerLootLog.Unheld(playerId, weaponId));
                }
            }

            var items = 0;
            var itemsGiven = 0;
            foreach (var item in box.Items)
            {
                items += item.Count;
                var put = _inventories.Give(playerId, item.ItemId, item.Count);
                itemsGiven += put;
                if (put > 0)
                {
                    Merge(given, item.ItemId, put);
                }
            }

            _log.WriteLine(ServerLootLog.Opened(box.ItemId, box.Kind, playerId, weaponsGiven, box.Weapons.Count, itemsGiven, items));
            if (given.Count > 0)
            {
                Given?.Invoke(playerId, given);
            }
        }

        // 武器 (付けた物は持ち物として) と持ち物を取り出して渡された並びに足し、持ち物の武器の写しも手放した武器に揃える
        private void TakeAll(int playerId, List<WeaponSpec> weapons, List<ItemCount> items)
        {
            foreach (var (spec, attachments) in _combat.WeaponsOf(playerId).Take())
            {
                weapons.Add(spec);
                foreach (var attachment in attachments)
                {
                    Merge(items, attachment, 1);
                }
            }

            foreach (var item in _inventories.Take(playerId))
            {
                Merge(items, item.ItemId, item.Count);
            }

            _inventories.HoldWeapons(playerId);
        }

        // 同じ試合の箱は種と置いた順で決まるので、地点ファイルの並びの順に引く。
        // Id を試合ごとに振り直さないのは、前の試合の箱へ送った開ける要求を、同じ Id の新しい箱への要求と取り違えないため
        private void HandleMatchStarted()
        {
            _boxes.Clear();
            _nextRefusalLogTimes.Clear();
            var map = _context.Map;
            var points = map.Points;
            if (points == null)
            {
                _log.WriteLine(ServerLootLog.NoPoints(map.Name));
                return;
            }

            foreach (var container in points.LootContainers)
            {
                var box = new Box(NextItemId(), ObjectiveItemKind.LootBox, container.Position, container.Reach, null);
                Fill(box, container.TableId, "map:" + container.Id);
                _boxes.Add(box);
            }

            _log.WriteLine(ServerLootLog.Placed(map.Name, _boxes.Count));
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _nextRefusalLogTimes.Remove(player.PlayerId);
        }

        // 表の空の Id は「表の無い箱」なので知らせない (オフラインの LootContainer が空のまま開けるのと同じ)
        private void Fill(Box box, string tableId, string source)
        {
            if (tableId.Length == 0)
            {
                return;
            }

            var table = _loot.Find(tableId);
            if (table == null)
            {
                _log.WriteLine(ServerLootLog.UnknownTable(source, tableId));
                return;
            }

            foreach (var rolled in LootRoll.Roll(table, _random))
            {
                if (!_items.TryFind(rolled.ItemId, out var spec))
                {
                    _log.WriteLine(ServerLootLog.UnknownItem(rolled.ItemId));
                    continue;
                }

                if (!spec.IsWeapon)
                {
                    Merge(box.Items, rolled.ItemId, rolled.Count);
                    continue;
                }

                // 同じ武器は 2 つ持てないので、数を引いても 1 つだけ入れる
                var weapon = WeaponFor(spec.WeaponId);
                if (weapon == null)
                {
                    _log.WriteLine(ServerLootLog.NotAWeapon(rolled.ItemId, spec.WeaponId));
                }
                else if (!box.Weapons.Contains(weapon))
                {
                    box.Weapons.Add(weapon);
                }
            }
        }

        // ルールが替わればロードアウトも替わるので、置くたびに今のロードアウトから引く
        private WeaponSpec WeaponFor(string weaponId)
        {
            var loadout = _rules.Loadout;
            if (loadout == null)
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

        private bool IsInReach(Box box, int playerId)
        {
            return _context.Bodies.TryGet(playerId, out var body) && HorizontalDistanceSquared(box.Position, body.Position) <= box.Reach * box.Reach;
        }

        private void RefuseLog(int itemId, int playerId, InteractRefusal refusal)
        {
            if (_nextRefusalLogTimes.TryGetValue(playerId, out var next) && _now < next)
            {
                return;
            }

            _nextRefusalLogTimes[playerId] = _now + _refusalLogInterval;
            _log.WriteLine(ServerLootLog.Refused(itemId, playerId, refusal));
        }

        private void RequireStarted()
        {
            if (_match == null)
            {
                throw new InvalidOperationException("the loot module is not running; place boxes between Start and Stop");
            }
        }

        private Box Find(int itemId)
        {
            foreach (var box in _boxes)
            {
                if (box.ItemId == itemId)
                {
                    return box;
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

        private static void Merge(List<ItemCount> items, string itemId, int count)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    items[i] = new ItemCount(itemId, (int)Math.Min((long)items[i].Count + count, int.MaxValue));
                    return;
                }
            }

            items.Add(new ItemCount(itemId, count));
        }

        private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (dx * dx) + (dz * dz);
        }

        private sealed class Box
        {
            public Box(int itemId, ObjectiveItemKind kind, Vector3 position, float reach, int? ownerId)
            {
                ItemId = itemId;
                Kind = kind;
                Position = position;
                Reach = reach;
                OwnerId = ownerId;
            }

            public int ItemId { get; }

            public ObjectiveItemKind Kind { get; }

            public Vector3 Position { get; }

            public float Reach { get; }

            public int? OwnerId { get; }

            public List<WeaponSpec> Weapons { get; } = new List<WeaponSpec>();

            public List<ItemCount> Items { get; } = new List<ItemCount>();

            public bool Opened { get; set; }
        }
    }
}
