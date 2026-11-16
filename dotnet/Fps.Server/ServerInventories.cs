using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // このサーバーには地面の拾い物が無いので、捨てたアイテムは消える
    internal sealed class ServerInventories : IServerModule
    {
        public const string LogTag = "[Inventory]";

        private readonly MatchRulesModule _rules;
        private readonly Dictionary<int, Inventory> _inventories = new Dictionary<int, Inventory>();
        private readonly HashSet<int> _dirty = new HashSet<int>();
        private readonly InventoryStateMessage _state = new InventoryStateMessage();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ItemCatalog _catalog;

        public ServerInventories(MatchRulesModule rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
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
            var rule = _catalog.Rule;
            _log.WriteLine(LogTag + " items=" + Text(_catalog.Items.Count) + " starting=" + Text(_catalog.Starting.Count)
                + " maxStacks=" + Text(rule.MaxStacks) + " maxWeight=" + Text(rule.MaxWeight)
                + " grid=" + Text(rule.GridWidth) + "x" + Text(rule.GridHeight) + " rotation=" + (rule.AllowRotation ? "on" : "off")
                + " carryLoad=" + (rule.CountWeightInCarryLoad ? "on" : "off"));
        }

        public void Tick(long tick, float deltaSeconds)
        {
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
                    body.CarriedItemWeight = _catalog.Rule.CountWeightInCarryLoad ? inventory.TotalWeight : 0f;
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
                    var moved = inventory.TryMove(request.Handle, request.X, request.Y, request.Rotated);
                    _log.WriteLine(LogTag + " move owner=" + Text(playerId) + " handle=" + Text(request.Handle) + " to=" + Text(request.X) + ","
                        + Text(request.Y) + (request.Rotated ? " rotated" : "") + " result=" + moved);
                    break;
                case InventoryAction.Use:
                    Use(playerId, inventory, request.Handle);
                    break;
                case InventoryAction.Drop:
                    Drop(playerId, inventory, request.Handle, request.Count);
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
        }

        internal Inventory InventoryOf(int playerId)
        {
            return _inventories.TryGetValue(playerId, out var inventory) ? inventory : null;
        }

        private void Use(int playerId, Inventory inventory, int handle)
        {
            var session = _rules.Session;
            ItemSpec item = null;
            if (inventory.TryGetStack(handle, out var stack))
            {
                inventory.Catalog.TryFind(stack.ItemId, out item);
            }

            ItemUseOutcome outcome;
            if (!_rules.CanFire(playerId) || session == null)
            {
                outcome = ItemUseOutcome.Rejected(ItemUseRejection.UserDown);
            }
            else
            {
                outcome = ItemUseJudge.Judge(item, new PickupCollector(null, session.GetHealth(playerId), session.MaxHealth));
            }

            if (!outcome.IsUsed)
            {
                _log.WriteLine(LogTag + " use owner=" + Text(playerId) + " handle=" + Text(handle) + " rejected=" + outcome.Rejection);
                return;
            }

            inventory.TryTake(handle, 1, out _);
            var healed = session.Heal(playerId, outcome.Health);
            _log.WriteLine(LogTag + " use owner=" + Text(playerId) + " item=" + item.Id + " health=+" + Text(healed));
        }

        private void Drop(int playerId, Inventory inventory, int handle, int count)
        {
            if (!_rules.CanFire(playerId) || count <= 0 || !inventory.TryTake(handle, count, out var dropped))
            {
                _log.WriteLine(LogTag + " drop owner=" + Text(playerId) + " handle=" + Text(handle) + " count=" + Text(count) + " rejected");
                return;
            }

            _log.WriteLine(LogTag + " drop owner=" + Text(playerId) + " item=" + dropped.ItemId + " count=" + Text(dropped.Count) + " discarded");
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
            GetOrAdd(player.PlayerId);
            _dirty.Add(player.PlayerId);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            _inventories.Remove(player.PlayerId);
            _dirty.Remove(player.PlayerId);
        }

        private void HandlePlayerSpawned(int playerId)
        {
            GetOrAdd(playerId).Fill();
            _dirty.Add(playerId);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
