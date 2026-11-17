using System.Numerics;

namespace Shooter.Core
{
    public readonly struct ZoneState
    {
        public int PhaseIndex { get; }

        public ZoneStage Stage { get; }

        public Vector2 Center { get; }

        public float Radius { get; }

        public Vector2 TargetCenter { get; }

        public float TargetRadius { get; }

        public float DamagePerSecond { get; }

        public float SecondsUntilShrink { get; }

        public float SecondsUntilStageEnds { get; }

        public ZoneState(
            int phaseIndex,
            ZoneStage stage,
            Vector2 center,
            float radius,
            Vector2 targetCenter,
            float targetRadius,
            float damagePerSecond,
            float secondsUntilShrink,
            float secondsUntilStageEnds)
        {
            PhaseIndex = phaseIndex;
            Stage = stage;
            Center = center;
            Radius = radius;
            TargetCenter = targetCenter;
            TargetRadius = targetRadius;
            DamagePerSecond = damagePerSecond;
            SecondsUntilShrink = secondsUntilShrink;
            SecondsUntilStageEnds = secondsUntilStageEnds;
        }

        public bool Contains(Vector2 position)
        {
            if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsInfinity(position.X) || float.IsInfinity(position.Y))
            {
                return false;
            }

            return Vector2.DistanceSquared(position, Center) <= Radius * Radius;
        }
    }
}
