using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // IBattleRoyaleWorld をサーバーのモジュールで果たす。どのモジュールも同じサーバーの一覧 (ServerModules) に並んだものを渡す
    internal sealed class ServerBattleRoyaleWorld : IBattleRoyaleWorld
    {
        private readonly ServerMovement _movement;
        private readonly ServerCombat _combat;
        private readonly ServerInventories _inventories;
        private readonly ServerLoot _loot;

        public ServerBattleRoyaleWorld(ServerMovement movement, ServerCombat combat, ServerInventories inventories, ServerLoot loot)
        {
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
        }

        // 持ち物の武器の写しも手放した武器に揃える (ServerLoot.SpawnDeathBox と同じ)
        public void Disarm(int playerId)
        {
            _combat.WeaponsOf(playerId).Clear();
            _inventories.HoldWeapons(playerId);
        }

        public bool IsArmed(int playerId)
        {
            return _combat.WeaponsOf(playerId).IsEquipped;
        }

        public bool Drop(int playerId, Vector3 position)
        {
            return _movement.Drop(playerId, position);
        }

        // 武器の Id は撃った人がいないダメージでは使われないので空にする
        public DamageOutcome HurtByZone(int playerId, int amount)
        {
            return _combat.DamageOverTime(null, playerId, amount, string.Empty);
        }

        public int SpawnCrate(Vector3 position, string tableId)
        {
            return _loot.SpawnCrate(position, tableId);
        }

        public int? SpawnDeathBox(int playerId)
        {
            return _loot.SpawnDeathBox(playerId);
        }

        public InteractVerdict Open(int playerId, int itemId)
        {
            return _loot.Open(playerId, itemId);
        }

        public void CollectUnopenedBoxes(List<ServerLootBox> boxes)
        {
            _loot.CollectUnopened(boxes);
        }
    }
}
