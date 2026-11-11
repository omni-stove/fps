using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// チームデスマッチでリスポーンする地点を選ぶ。乱数は使わず、同じ入力なら同じ地点を返す。
    /// </summary>
    /// <remarks>
    /// 1. 自分のチーム用か、どちらのチームも使える地点に絞る。
    /// 2. 敵が MinEnemyDistance 以内にいる地点を除く。
    /// 3. 残りから「いちばん近い敵までの距離」が最大の地点を選ぶ。同点なら味方に近いほう、それも同点なら一覧で先の地点。
    /// 4. 2 ですべて除外されたら、1 の後の地点から 3 と同じ決め方で選ぶ。
    /// </remarks>
    public sealed class SpawnSelector
    {
        /// <summary>MinEnemyDistance の既定値 (m)。</summary>
        public const float DefaultMinEnemyDistance = 15f;

        /// <summary>この距離 (m) 以内に敵がいる地点は、他に候補があれば選ばない。</summary>
        public float MinEnemyDistance { get; }

        /// <param name="minEnemyDistance">敵を避ける距離 (m)。0 以上の有限の値。</param>
        public SpawnSelector(float minEnemyDistance = DefaultMinEnemyDistance)
        {
            if (float.IsNaN(minEnemyDistance) || float.IsInfinity(minEnemyDistance) || minEnemyDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minEnemyDistance), minEnemyDistance, "minEnemyDistance must be finite and not negative");
            }

            MinEnemyDistance = minEnemyDistance;
        }

        /// <summary>リスポーンする地点を選ぶ。</summary>
        /// <param name="spawnPoints">リスポーン地点の一覧。</param>
        /// <param name="enemyPositions">敵の位置の一覧。空でもよい。</param>
        /// <param name="allyPositions">味方の位置の一覧 (リスポーンするプレイヤー自身は含めない)。空でもよい。</param>
        /// <param name="team">リスポーンするプレイヤーのチーム。</param>
        /// <exception cref="InvalidOperationException">そのチームが使える地点が 1 件も無い。</exception>
        public SpawnPoint Select(
            IReadOnlyList<SpawnPoint> spawnPoints,
            IReadOnlyList<Vector3> enemyPositions,
            IReadOnlyList<Vector3> allyPositions,
            Team team)
        {
            if (spawnPoints == null)
            {
                throw new ArgumentNullException(nameof(spawnPoints));
            }

            if (enemyPositions == null)
            {
                throw new ArgumentNullException(nameof(enemyPositions));
            }

            if (allyPositions == null)
            {
                throw new ArgumentNullException(nameof(allyPositions));
            }

            if (!Enum.IsDefined(typeof(Team), team))
            {
                throw new ArgumentOutOfRangeException(nameof(team), team, "team is not defined");
            }

            if (spawnPoints.Count == 0)
            {
                throw new InvalidOperationException("no spawn points");
            }

            var minEnemyDistanceSquared = MinEnemyDistance * MinEnemyDistance;
            Candidate best = null;
            Candidate fallback = null;

            for (var i = 0; i < spawnPoints.Count; i++)
            {
                var point = spawnPoints[i];
                if (point == null)
                {
                    throw new ArgumentException("spawnPoints must not contain null", nameof(spawnPoints));
                }

                if (!point.IsUsableBy(team))
                {
                    continue;
                }

                var candidate = new Candidate(
                    point,
                    NearestDistanceSquared(point.Position, enemyPositions),
                    NearestDistanceSquared(point.Position, allyPositions));

                if (IsBetter(candidate, fallback))
                {
                    fallback = candidate;
                }

                if (candidate.EnemyDistanceSquared > minEnemyDistanceSquared && IsBetter(candidate, best))
                {
                    best = candidate;
                }
            }

            if (fallback == null)
            {
                throw new InvalidOperationException($"no spawn points usable by team {team}");
            }

            return (best ?? fallback).Point;
        }

        /// <summary>
        /// candidate が current より良いか。敵から遠いほう、同点なら味方に近いほうが良い。
        /// それも同点なら先に見た current を残す。
        /// </summary>
        private static bool IsBetter(Candidate candidate, Candidate current)
        {
            if (current == null)
            {
                return true;
            }

            if (candidate.EnemyDistanceSquared != current.EnemyDistanceSquared)
            {
                return candidate.EnemyDistanceSquared > current.EnemyDistanceSquared;
            }

            return candidate.AllyDistanceSquared < current.AllyDistanceSquared;
        }

        /// <summary>positions のうち最も近いものまでの距離の 2 乗。空なら正の無限大。</summary>
        private static float NearestDistanceSquared(Vector3 from, IReadOnlyList<Vector3> positions)
        {
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < positions.Count; i++)
            {
                nearest = Math.Min(nearest, Vector3.DistanceSquared(from, positions[i]));
            }

            return nearest;
        }

        private sealed class Candidate
        {
            public SpawnPoint Point { get; }
            public float EnemyDistanceSquared { get; }
            public float AllyDistanceSquared { get; }

            public Candidate(SpawnPoint point, float enemyDistanceSquared, float allyDistanceSquared)
            {
                Point = point;
                EnemyDistanceSquared = enemyDistanceSquared;
                AllyDistanceSquared = allyDistanceSquared;
            }
        }
    }
}
