using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class SpawnSelector
    {
        public const float DefaultMinEnemyDistance = 15f;

        public float MinEnemyDistance { get; }

        public SpawnSelector(float minEnemyDistance = DefaultMinEnemyDistance)
        {
            if (float.IsNaN(minEnemyDistance) || float.IsInfinity(minEnemyDistance) || minEnemyDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minEnemyDistance), minEnemyDistance, "minEnemyDistance must be finite and not negative");
            }

            MinEnemyDistance = minEnemyDistance;
        }

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
