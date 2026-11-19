using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct SquadSpawnMate
    {
        // 撃たれた時刻でなく経過秒で持つのは、呼ぶ側の時計 (Time.time やサーバーの刻み) に Core を縛らないため。
        public const float NeverDamaged = float.PositiveInfinity;

        public SquadSpawnMate(Side squad, Vector3 position, bool isAlive, float secondsSinceDamaged = NeverDamaged)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!(secondsSinceDamaged >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsSinceDamaged), secondsSinceDamaged, "secondsSinceDamaged must not be negative or NaN");
            }

            Squad = squad;
            Position = position;
            IsAlive = isAlive;
            SecondsSinceDamaged = secondsSinceDamaged;
        }

        public Side Squad { get; }

        public Vector3 Position { get; }

        public bool IsAlive { get; }

        public float SecondsSinceDamaged { get; }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
