using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class SmokeScreen
    {
        // 傷も状態異常も与えない範囲には視線を遮るほかに効き目が無いので、種類の印を足さず中身で見分ける
        public static bool IsSmoke(AreaEffectSpec spec)
        {
            return spec != null && spec.DamagePerTick == 0 && spec.StatusEffect == null;
        }

        public static bool Blocks(Vector3 center, float radius, Vector3 from, Vector3 to)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            var line = to - from;
            var lengthSq = Vector3.Dot(line, line);
            var along = lengthSq > 0f ? Math.Clamp(Vector3.Dot(center - from, line) / lengthSq, 0f, 1f) : 0f;
            var closest = from + (line * along);
            return Vector3.DistanceSquared(closest, center) < radius * radius;
        }
    }
}
