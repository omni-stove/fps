using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // このサーバーには地面の拾い物が無いので、捨てたアイテムは消える。
    // 武器はルールが選んだ並びを出現のたびに持たせ直すので、持ち物からは捨てられない (使うとその武器に持ち替える)
    internal sealed class ServerInventories : IServerModule
    {
        public const string LogTag = "[Inventory]";

        // 断った理由のログを出す最短の間隔 (秒)。押し続けてログを埋めないため (ServerLoot と同じ)
        private const float _refusalLogInterval = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerCombat _combat;
        private readonly Dictionary<int, Inventory> _inventories = new Dictionary<int, Inventory>();
        private readonly HashSet<int> _dirty = new HashSet<int>();
        private readonly Dictionary<int, float> _nextRefusalLogTimes = new Dictionary<int, float>();
        private readonly InventoryStateMessage _state = new InventoryStateMessage();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ItemCatalog _catalog;
        private float _now;

        public ServerInventories(MatchRulesModule rules, ServerCombat combat = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _combat = combat;
        }

        public string Name => "inventory";

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _catalog = context.Rules?.Items ?? ItemCatalog.Empty;
            _match.PlayerJoined += HandlePlayerJoined;
            _match.PlayerLeft += HandlePlayerLeft;
            _rules.PlayerSpawned += HandlePlayerSpawned;
            _log.WriteLine(ServerInventoryLog.Started(_catalog));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            if (_dirty.Count == 0)
            {
                return;
            }

            foreach (var playerId in _dirty)
            {
                if (!_inventories.TryGetValue(playerId, out var inventory))
                {
                    continue;
                }

                if (_context.Bodies.TryGet(playerId, out var body))
                {
                    // 武器の重さは移動のルール (CarryLoadSpec) が武器として数えるので、持ち物の重さからは除く
                    body.CarriedItemWeight = _catalog.Rule.CountWeightInCarryLoad ? inventory.TotalWeight - inventory.WeaponWeight : 0f;
                }

                if (_match.TryGetPlayer(playerId, out var player))
                {
                    InventorySync.Write(inventory, _state);
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _state);
                }
            }

            _dirty.Clear();
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is InventoryRequestMessage request)
                || !_match.TryGetPlayer(connection, out var player)
                || !_inventories.TryGetValue(player.PlayerId, out var inventory))
            {
                return;
            }

            var playerId = player.PlayerId;

            // 断ったときもクライアントが先に動かした見た目を戻せるよう、今の中身を送り直す
            _dirty.Add(playerId);
            switch (request.Action)
            {
                case InventoryAction.Move:
                    Move(playerId, inventory, request);
                    break;
                case InventoryAction.Use:
                    Use(playerId, inventory, request.Handle);
                    break;
                case InventoryAction.Drop:
                    Drop(playerId, inventory, request.Handle, request.Count);
                    break;
                case InventoryAction.Attach:
                    Attach(playerId, inventory, request.Handle);
                    break;
                case InventoryAction.Detach:
                    Detach(playerId, inventory, request.Handle, request.ItemId);
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
            _rules.PlayerSpawned -= HandlePlayerSpawned;
            _inventories.Clear();
            _dirty.Clear();
            _nextRefusalLogTimes.Clear();
        }

        internal Inventory InventoryOf(int playerId)
        {
            return _inventories.TryGetValue(playerId, out var inventory) ? inventory : null;
        }

        // 箱の中身・持ち込みのように、目的が外から持ち物に入れる口。入った数を返し、入り切らない分は入れない。
        // 武器は ServerPlayerWeapons.Give で持たせる (持ち物の武器は持っている武器の写しなので、ここからは入れない)
        internal int Give(int playerId, string itemId, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            if (!_inventories.TryGetValue(playerId, out var inventory))
            {
                return 0;
            }

            var given = inventory.Add(itemId, count);
            _log.WriteLine(ServerInventoryLog.Give(playerId, itemId, count, given));
            return given;
        }

        // ServerPlayerWeapons.Give・Clear・Take は持ち物を知らないので、その後にこれで持ち物の武器の写しを今の武器に揃える。返すのは持ち物に入らなかった武器
        internal IReadOnlyList<string> HoldWeapons(int playerId)
        {
            if (_combat == null || !_inventories.TryGetValue(playerId, out var inventory))
            {
                return Array.Empty<string>();
            }

            var weapons = _combat.WeaponsOf(playerId);
            return inventory.HoldWeapons(weapons.Specs, weapons.AttachmentsOf);
        }

        // 持ち物を空にする。武器の写しは残すのは、武器を手放すかは ServerPlayerWeapons が決めるため
        internal void Clear(int playerId)
        {
            Take(playerId, null);
        }

        // 武器でない物をすべて取り出し、並びの順に返す。倒れた人の箱・持ち帰りに使う
        internal IReadOnlyList<ItemCount> Take(int playerId)
        {
            var taken = new List<ItemCount>();
            Take(playerId, taken);
            return taken;
        }

        private void Take(int playerId, List<ItemCount> taken)
        {
            if (!_inventories.TryGetValue(playerId, out var inventory))
            {
                return;
            }

            var handles = new List<int>();
            foreach (var stack in inventory.Stacks)
            {
                if (inventory.WeaponIdOf(stack.Handle) == null)
                {
                    handles.Add(stack.Handle);
                }
            }

            var count = 0;
            foreach (var handle in handles)
            {
                if (inventory.TryGetStack(handle, out var stack) && inventory.TryTake(handle, stack.Count, out var item))
                {
                    taken?.Add(item);
                    count += item.Count;
                }
            }

            _log.WriteLine(taken == null ? ServerInventoryLog.Clear(playerId, count) : ServerInventoryLog.Take(playerId, taken.Count, count));
        }

        // 並べ替えは持っている物を増やしも減らしもしないので、倒れている人や観戦している人にも許す (オフラインの PlayerInventory.Move と同じ)
        private void Move(int playerId, Inventory inventory, InventoryRequestMessage request)
        {
            var moved = inventory.TryMove(request.Handle, request.X, request.Y, request.Rotated);
            if (moved == InventoryMoveResult.Moved || ShouldLogRefusal(playerId))
            {
                _log.WriteLine(ServerInventoryLog.Move(playerId, request.Handle, request.X, request.Y, request.Rotated, moved.ToString()));
            }
        }

        private void Use(int playerId, Inventory inventory, int handle)
        {
            ItemSpec item = null;
            if (inventory.TryGetStack(handle, out var stack))
            {
                inventory.Catalog.TryFind(stack.ItemId, out item);
            }

            var refusal = RefusalOf(playerId);
            if (item != null && item.IsWeapon)
            {
                Equip(playerId, item.WeaponId, refusal);
                return;
            }

            if (refusal != ServerInventoryRefusal.None)
            {
                RefuseUse(playerId, handle, refusal.ToString());
                return;
            }

            var session = _rules.Session;
            var outcome = ItemUseJudge.Judge(item, new PickupCollector(null, session.GetHealth(playerId), session.MaxHealth));
            if (!outcome.IsUsed)
            {
                RefuseUse(playerId, handle, outcome.Rejection.ToString());
                return;
            }

            if (!inventory.TryTake(handle, 1, out _))
            {
                RefuseUse(playerId, handle, ItemUseRejection.NoItem.ToString());
                return;
            }

            var healed = session.Heal(playerId, outcome.Health);
            _log.WriteLine(ServerInventoryLog.Used(playerId, item.Id, healed));
        }

        // 持ち替えの間合い (しまう・抜く秒数) は Loadout.TrySelect が持つので、ここでは持っているかと今の状態だけを見る
        private void Equip(int playerId, string weaponId, ServerInventoryRefusal refusal)
        {
            string reason = null;
            if (refusal != ServerInventoryRefusal.None)
            {
                reason = refusal.ToString();
            }
            else if (_combat == null)
            {
                reason = "NotHeld";
            }
            else
            {
                var weapons = _combat.WeaponsOf(playerId);
                if (string.Equals(weapons.CurrentSpec?.Id, weaponId, StringComparison.Ordinal))
                {
                    reason = "AlreadyEquipped";
                }
                else if (!weapons.TrySwitch(weaponId))
                {
                    reason = "NotHeld";
                }
            }

            if (reason == null)
            {
                _log.WriteLine(ServerInventoryLog.Equipped(playerId, weaponId));
            }
            else if (ShouldLogRefusal(playerId))
            {
                _log.WriteLine(ServerInventoryLog.EquipRefused(playerId, weaponId, reason));
            }
        }

        // 頼む数が山より多いときは山の数だけ捨てる。捨てるだけで何も得ないので、断らずに縮める (オフラインと同じ)
        private void Drop(int playerId, Inventory inventory, int handle, int count)
        {
            var weaponId = inventory.WeaponIdOf(handle);
            if (weaponId != null)
            {
                if (ShouldLogRefusal(playerId))
                {
                    _log.WriteLine(ServerInventoryLog.DropWeaponRefused(playerId, weaponId));
                }

                return;
            }

            var refusal = RefusalOf(playerId);
            var reason = refusal != ServerInventoryRefusal.None ? refusal.ToString() : count <= 0 ? "Count" : null;
            if (reason == null && inventory.TryTake(handle, count, out var dropped))
            {
                _log.WriteLine(ServerInventoryLog.Dropped(playerId, dropped.ItemId, dropped.Count));
                return;
            }

            if (ShouldLogRefusal(playerId))
            {
                _log.WriteLine(ServerInventoryLog.DropRefused(playerId, handle, count, reason ?? "NoStack"));
            }
        }

        private void Attach(int playerId, Inventory inventory, int handle)
        {
            var refusal = RefusalOf(playerId);
            if (refusal != ServerInventoryRefusal.None)
            {
                if (ShouldLogRefusal(playerId))
                {
                    _log.WriteLine(ServerInventoryLog.Attach(playerId, handle, null, null, null, refusal.ToString()));
                }

                return;
            }

            var weapons = _combat?.WeaponsOf(playerId);
            var weaponId = weapons?.CurrentSpec?.Id;
            var result = InventoryAttachments.Attach(
                inventory,
                handle,
                weaponId,
                weapons != null ? weapons.AttachmentsOf(weaponId) : WeaponAttachments.None,
                next => SetAttachments(weapons, inventory, weaponId, next),
                out var itemId,
                out var replaced);
            if (result == AttachmentRejection.None)
            {
                _log.WriteLine(ServerInventoryLog.Attach(playerId, handle, itemId, weaponId, replaced, null));
            }
            else if (ShouldLogRefusal(playerId))
            {
                _log.WriteLine(ServerInventoryLog.Attach(playerId, handle, itemId, weaponId, replaced, result.ToString()));
            }
        }

        private void Detach(int playerId, Inventory inventory, int weaponHandle, string itemId)
        {
            // 届いた Id は送ってきた側の文字列 (改行も入れられる) なので、一覧にある物だけをログに出す (MatchRulesModule.ListedItemId と同じ)
            var listedItemId = inventory.Catalog.TryFind(itemId, out var listed) ? listed.Id : null;
            var refusal = RefusalOf(playerId);
            if (refusal != ServerInventoryRefusal.None)
            {
                if (ShouldLogRefusal(playerId))
                {
                    _log.WriteLine(ServerInventoryLog.Detach(playerId, null, listedItemId, refusal.ToString()));
                }

                return;
            }

            var weapons = _combat?.WeaponsOf(playerId);
            var weaponId = weapons != null ? inventory.WeaponIdOf(weaponHandle) : null;
            var result = InventoryAttachments.Detach(
                inventory,
                weaponId,
                weapons != null ? weapons.AttachmentsOf(weaponId) : WeaponAttachments.None,
                itemId,
                next => SetAttachments(weapons, inventory, weaponId, next));
            if (result == AttachmentRejection.None)
            {
                _log.WriteLine(ServerInventoryLog.Detach(playerId, weaponId, listedItemId, null));
            }
            else if (ShouldLogRefusal(playerId))
            {
                _log.WriteLine(ServerInventoryLog.Detach(playerId, weaponId, listedItemId, result.ToString()));
            }
        }

        // 持ち物を増やす・減らす・付け替える要求は、撃てる人 (MatchRulesModule.CanFire) にだけ許す。断るときは撃てない訳を返す
        private ServerInventoryRefusal RefusalOf(int playerId)
        {
            if (_rules.CanFire(playerId))
            {
                return ServerInventoryRefusal.None;
            }

            var session = _rules.Session;
            if (session == null || !session.HasPlayer(playerId))
            {
                return ServerInventoryRefusal.NotInMatch;
            }

            if (session.State != GameModeState.Playing)
            {
                return ServerInventoryRefusal.NotPlaying;
            }

            if (!session.IsAlive(playerId))
            {
                return ServerInventoryRefusal.Dead;
            }

            return session.IsDowned(playerId) ? ServerInventoryRefusal.Downed : ServerInventoryRefusal.BetweenRounds;
        }

        private void RefuseUse(int playerId, int handle, string reason)
        {
            if (ShouldLogRefusal(playerId))
            {
                _log.WriteLine(ServerInventoryLog.UseRefused(playerId, handle, reason));
            }
        }

        private bool ShouldLogRefusal(int playerId)
        {
            if (_nextRefusalLogTimes.TryGetValue(playerId, out var next) && _now < next)
            {
                return false;
            }

            _nextRefusalLogTimes[playerId] = _now + _refusalLogInterval;
            return true;
        }

        // 持ち物の武器の重さは武器の値から取るので、武器の値を替えてから揃える
        private static bool SetAttachments(ServerPlayerWeapons weapons, Inventory inventory, string weaponId, IReadOnlyList<string> next)
        {
            if (!weapons.SetAttachments(weaponId, next, inventory.Catalog))
            {
                return false;
            }

            inventory.HoldWeapons(weapons.Specs, weapons.AttachmentsOf);
            return true;
        }

        private Inventory GetOrAdd(int playerId)
        {
            if (!_inventories.TryGetValue(playerId, out var inventory))
            {
                inventory = new Inventory(_catalog);
                inventory.Changed += () => _dirty.Add(playerId);
                _inventories.Add(playerId, inventory);
            }

            return inventory;
        }

        // 入り直した人にも今の中身を送る
        private void HandlePlayerJoined(ServerPlayer player)
        {
            var inventory = GetOrAdd(player.PlayerId);
            _combat?.WeaponsOf(player.PlayerId).UseAmmoPools(spec => InventoryWeapons.PoolFor(inventory, spec));
            _dirty.Add(player.PlayerId);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _inventories.Remove(player.PlayerId);
            _dirty.Remove(player.PlayerId);
            _nextRefusalLogTimes.Remove(player.PlayerId);
        }

        // 戦闘が先に武器を持たせ直している (モジュールの順) ので、その武器を持ち物に置き、最初の予備弾を弾薬で持たせる
        private void HandlePlayerSpawned(int playerId)
        {
            var inventory = GetOrAdd(playerId);
            // Fill が武器の重さを使って最初の持ち物を置くので、出直しで外れた付けた物の重さを先に武器へ反映する
            if (_combat != null)
            {
                var spawned = _combat.WeaponsOf(playerId);
                inventory.HoldWeapons(spawned.Specs, spawned.AttachmentsOf);
            }

            inventory.Fill();
            if (_combat != null)
            {
                var weapons = _combat.WeaponsOf(playerId);
                foreach (var weaponId in inventory.HoldWeapons(weapons.Specs, weapons.AttachmentsOf))
                {
                    _log.WriteLine(ServerInventoryLog.WeaponOutside(playerId, weaponId));
                }

                var restocked = InventoryWeapons.Restock(inventory, weapons.PooledAmmo());
                if (restocked > 0)
                {
                    _log.WriteLine(ServerInventoryLog.Restock(playerId, restocked));
                }
            }

            _dirty.Add(playerId);
        }
    }
}
