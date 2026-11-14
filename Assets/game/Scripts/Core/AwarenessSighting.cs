using System;

namespace Fps.Core
{
    public readonly struct AwarenessSighting
    {
        public static AwarenessSighting None => default;

        public AwarenessSighting(
            bool hasLineOfSight, float distance, float angleDegrees, AwarenessPosture posture, float lightFactor, float speed)
        {
            RequireNonNegative(distance, nameof(distance));
            RequireNonNegative(angleDegrees, nameof(angleDegrees));
            RequireNonNegative(speed, nameof(speed));
            if (angleDegrees > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(angleDegrees), angleDegrees, "angleDegrees must be at most 180");
            }

            if (!(lightFactor >= 0f) || lightFactor > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(lightFactor), lightFactor, "lightFactor must be between 0 and 1");
            }

            if (!Enum.IsDefined(typeof(AwarenessPosture), posture))
            {
                throw new ArgumentOutOfRangeException(nameof(posture), posture, "unknown posture");
            }

            HasLineOfSight = hasLineOfSight;
            Distance = distance;
            AngleDegrees = angleDegrees;
            Posture = posture;
            LightFactor = lightFactor;
            Speed = speed;
        }

        public bool HasLineOfSight { get; }

        public float Distance { get; }

        // 敵の正面と目標の方向のなす角。左右を区別しないので 0〜180 にそろえる。
        public float AngleDegrees { get; }

        public AwarenessPosture Posture { get; }

        // 光源から明るさを測る仕組みはキットに無いので、ゲームが場所ごとに決めた係数をそのまま受け取る。
        public float LightFactor { get; }

        public float Speed { get; }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative and must be finite");
            }
        }
    }
}
