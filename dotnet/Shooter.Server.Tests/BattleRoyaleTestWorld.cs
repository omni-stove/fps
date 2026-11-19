using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // BR の目的だけを確かめるため、移動・武器・箱のモジュールの代わりに頼まれたことを覚える。
    // エリアのダメージだけは、ServerCombat.DamageOverTime と同じく撃った人のいないダメージ (本人から本人) として Session に通す
    internal sealed class BattleRoyaleTestWorld : IBattleRoyaleWorld
    {
        private readonly Func<TeamDeathmatchSession> _session;

        public BattleRoyaleTestWorld(Func<TeamDeathmatchSession> session = null)
        {
            _session = session ?? (() => null);
        }

        public List<int> Disarmed { get; } = new List<int>();

        public HashSet<int> Armed { get; } = new HashSet<int>();

        public List<(int PlayerId, Vector3 From)> Drops { get; } = new List<(int PlayerId, Vector3 From)>();

        public List<(Vector3 Position, string TableId)> Crates { get; } = new List<(Vector3 Position, string TableId)>();

        public List<int> DeathBoxes { get; } = new List<int>();

        public List<(int PlayerId, int ItemId)> Opened { get; } = new List<(int PlayerId, int ItemId)>();

        // まだ開けていない箱として見せる物
        public List<ServerLootBox> Boxes { get; } = new List<ServerLootBox>();

        public void Disarm(int playerId)
        {
            Disarmed.Add(playerId);
            Armed.Remove(playerId);
        }

        public bool IsArmed(int playerId) => Armed.Contains(playerId);

        public bool Drop(int playerId, Vector3 position)
        {
            Drops.Add((playerId, position));
            return true;
        }

        public DamageOutcome HurtByZone(int playerId, int amount)
        {
            var session = _session();
            return session == null ? DamageOutcome.Ignored : session.ApplyDamage(playerId, playerId, amount);
        }

        public List<float> CrateReaches { get; } = new List<float>();

        public int SpawnCrate(Vector3 position, string tableId, float reach)
        {
            Crates.Add((position, tableId));
            CrateReaches.Add(reach);
            return 1000 + Crates.Count;
        }

        public int? SpawnDeathBox(int playerId)
        {
            DeathBoxes.Add(playerId);
            return 2000 + DeathBoxes.Count;
        }

        public InteractVerdict Open(int playerId, int itemId)
        {
            Opened.Add((playerId, itemId));
            Boxes.RemoveAll(box => box.ItemId == itemId);
            return InteractVerdict.Accepted;
        }

        public void CollectUnopenedBoxes(List<ServerLootBox> boxes)
        {
            boxes.AddRange(Boxes);
        }
    }
}
