using System.Collections.Generic;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    public sealed class DebrisCrushSmokeStep : ITpsActionSmokeStep
    {
        public const string LogTag = "[DebrisCrushSmoke]";

        private readonly List<string> _killFeedLines = new List<string>();
        private bool _sawCrush;
        private bool _broke;
        private Damageable _enemy;
        private string _expectedKillFeed;

        public int Order => 100;

        public string Name => "debris crush";

        public float TimeoutSeconds => 10f;

        public void Reset()
        {
            _killFeedLines.Clear();
            _sawCrush = false;
            _broke = false;
            _enemy = null;
            _expectedKillFeed = null;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(DebrisCrusher.LogTag + " debris hit", System.StringComparison.Ordinal)
                && condition.Contains(" id=" + DebrisCrushRangeFixture.EnemyId + " "))
            {
                _sawCrush = true;
            }

            if (condition.StartsWith(KillFeedView.LogTag, System.StringComparison.Ordinal))
            {
                _killFeedLines.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            if (!_broke)
            {
                problem = BreakPillar(world);
                return false;
            }

            if (!_sawCrush || !_enemy.IsDead)
            {
                return false;
            }

            foreach (var line in _killFeedLines)
            {
                if (line.Contains(_expectedKillFeed))
                {
                    Debug.Log($"{LogTag} the debris killed {DebrisCrushRangeFixture.EnemyId} and the kill feed credits the player: {line}");
                    return true;
                }
            }

            return false;
        }

        public void Cleanup()
        {
        }

        private string BreakPillar(BootstrapWorld world)
        {
            var root = GameObject.Find(DebrisCrushRangeFixture.RootName);
            if (root == null)
            {
                return $"'{DebrisCrushRangeFixture.RootName}' is not in the TPS range; rebuild the scene with TpsRangeSceneBuilder";
            }

            var pillar = root.GetComponentInChildren<DestructibleStructure>();
            if (pillar == null || !pillar.DestructionEnabled || !pillar.DebrisCrushEnabled)
            {
                return $"'{DebrisCrushRangeFixture.PillarName}' is missing or does not break with crushing debris";
            }

            _enemy = null;
            foreach (var damageable in root.GetComponentsInChildren<Damageable>())
            {
                if (damageable.Id == DebrisCrushRangeFixture.EnemyId)
                {
                    _enemy = damageable;
                }
            }

            if (_enemy == null || _enemy.IsDead)
            {
                return $"'{DebrisCrushRangeFixture.EnemyName}' is missing or already dead";
            }

            var player = world.Player;
            var weapon = world.Weapon != null ? world.Weapon.Weapon : null;
            var weaponId = weapon != null ? weapon.Spec.Id : string.Empty;
            _expectedKillFeed = $" killer={CombatEvents.OfflineId(player)} victim={CombatEvents.OfflineId(_enemy.gameObject)} ";

            var piece = pillar.Pieces[DebrisCrushRangeFixture.BaseIndex];

            // 根元の破片が敵と反対へ飛ぶよう、敵の側の面に当てる。
            var point = piece.transform.position + (Vector3.right * (DebrisCrushRangeFixture.PieceSize / 2f));
            int hits;
            using (CombatEvents.Attribute(player, weaponId))
            {
                hits = StructureDamage.Apply(piece.Collider, point, piece.MaxHealth, 0f);
            }

            if (hits == 0 || !pillar.Integrity.IsDestroyed(DebrisCrushRangeFixture.BaseIndex))
            {
                return $"hitting the base of '{DebrisCrushRangeFixture.PillarName}' did not break it";
            }

            _broke = true;
            Debug.Log($"{LogTag} broke the base of {DebrisCrushRangeFixture.PillarName} as {player.name} weapon={weaponId}");
            return null;
        }
    }
}
