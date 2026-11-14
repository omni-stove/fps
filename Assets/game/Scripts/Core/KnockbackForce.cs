using System;

namespace Fps.Core
{
    public readonly struct KnockbackForce
    {
        // 重さは割合ではなく累乗で効かせる。0 で重さを見ず、1 で運動量を保つ (重さに反比例する) ように、1 つの係数で両端を表せるため。
        public KnockbackForce(float speed, float upwardRatio, float weightFactor)
        {
            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be a non-negative finite number");
            }

            if (!(upwardRatio >= 0f && upwardRatio <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(upwardRatio), upwardRatio, "upwardRatio must be within [0, 1]");
            }

            if (!(weightFactor >= 0f) || float.IsInfinity(weightFactor))
            {
                throw new ArgumentOutOfRangeException(nameof(weightFactor), weightFactor, "weightFactor must be a non-negative finite number");
            }

            Speed = speed;
            UpwardRatio = upwardRatio;
            WeightFactor = weightFactor;
        }

        public static KnockbackForce None => default;

        public float Speed { get; }

        public float UpwardRatio { get; }

        public float WeightFactor { get; }
    }
}
