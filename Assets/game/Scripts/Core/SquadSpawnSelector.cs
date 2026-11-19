using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class SquadSpawnSelector
    {
        public SquadSpawnSpec Spec { get; }

        public SquadSpawnSelector(SquadSpawnSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        // 地形や壁との重なりは Core では分からないので isClear で呼ぶ側 (Runtime の物理) に尋ねる。null なら体どうしの重なりだけを見る。
        public Vector3? Select(
            Side squad,
            IReadOnlyList<SquadSpawnMate> mates,
            IReadOnlyList<Vector3> enemyPositions,
            Func<Vector3, bool> isClear = null)
        {
            if (mates == null)
            {
                throw new ArgumentNullException(nameof(mates));
            }

            if (enemyPositions == null)
            {
                throw new ArgumentNullException(nameof(enemyPositions));
            }

            var minEnemyDistanceSquared = Spec.MinEnemyDistance * Spec.MinEnemyDistance;
            var safe = new List<Candidate>();
            for (var i = 0; i < mates.Count; i++)
            {
                var mate = mates[i];
                if (mate.Squad != squad || !mate.IsAlive || mate.SecondsSinceDamaged < Spec.DamageCooldownSeconds)
                {
                    continue;
                }

                var enemyDistanceSquared = NearestDistanceSquared(mate.Position, enemyPositions);
                if (enemyDistanceSquared < minEnemyDistanceSquared)
                {
                    continue;
                }

                safe.Add(new Candidate(i, enemyDistanceSquared));
            }

            // List.Sort は安定でないので、同点では添字も比べて同じ入力なら毎回同じ味方を選ぶ。
            safe.Sort((a, b) => a.EnemyDistanceSquared != b.EnemyDistanceSquared
                ? b.EnemyDistanceSquared.CompareTo(a.EnemyDistanceSquared)
                : a.Index.CompareTo(b.Index));

            for (var i = 0; i < safe.Count; i++)
            {
                var slot = FreeSlot(mates[safe[i].Index].Position, mates, enemyPositions, minEnemyDistanceSquared, isClear);
                if (slot.HasValue)
                {
                    return slot;
                }
            }

            return null;
        }

        private Vector3? FreeSlot(
            Vector3 around,
            IReadOnlyList<SquadSpawnMate> mates,
            IReadOnlyList<Vector3> enemyPositions,
            float minEnemyDistanceSquared,
            Func<Vector3, bool> isClear)
        {
            var clearanceSquared = Spec.Clearance * Spec.Clearance;
            Vector3? best = null;
            var bestEnemyDistanceSquared = float.NegativeInfinity;
            for (var k = 0; k < Spec.SlotCount; k++)
            {
                var angle = 2.0 * Math.PI * k / Spec.SlotCount;
                var position = around + new Vector3(
                    (float)(Math.Cos(angle) * Spec.MateDistance),
                    0f,
                    (float)(Math.Sin(angle) * Spec.MateDistance));

                // 味方が安全でも、敵の側へずれた位置は敵に近すぎることがあるので位置ごとにも確かめる。
                var enemyDistanceSquared = NearestDistanceSquared(position, enemyPositions);
                if (enemyDistanceSquared < minEnemyDistanceSquared || enemyDistanceSquared < clearanceSquared)
                {
                    continue;
                }

                if (IsOccupiedByMate(position, mates, clearanceSquared))
                {
                    continue;
                }

                if (isClear != null && !isClear(position))
                {
                    continue;
                }

                if (enemyDistanceSquared > bestEnemyDistanceSquared)
                {
                    best = position;
                    bestEnemyDistanceSquared = enemyDistanceSquared;
                }
            }

            return best;
        }

        // 他の分隊の体とも重ならないよう、分隊を問わず生きている体をすべて見る。
        private static bool IsOccupiedByMate(Vector3 position, IReadOnlyList<SquadSpawnMate> mates, float clearanceSquared)
        {
            for (var i = 0; i < mates.Count; i++)
            {
                if (mates[i].IsAlive && Vector3.DistanceSquared(position, mates[i].Position) < clearanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static float NearestDistanceSquared(Vector3 from, IReadOnlyList<Vector3> positions)
        {
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < positions.Count; i++)
            {
                nearest = Math.Min(nearest, Vector3.DistanceSquared(from, positions[i]));
            }

            return nearest;
        }

        private readonly struct Candidate
        {
            public Candidate(int index, float enemyDistanceSquared)
            {
                Index = index;
                EnemyDistanceSquared = enemyDistanceSquared;
            }

            public int Index { get; }

            public float EnemyDistanceSquared { get; }
        }
    }
}
