using System;

namespace Shooter.Core
{
    public sealed class LookAngles
    {
        // 真上・真下を向いてジンバルが乱れないよう、既定では ±85° で止める。
        public const float DefaultMaxPitch = 85f;

        public float MaxPitch { get; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        public LookAngles(float maxPitch = DefaultMaxPitch)
        {
            if (!(maxPitch > 0f && maxPitch <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxPitch), maxPitch, "maxPitch must be in (0, 90]");
            }

            MaxPitch = maxPitch;
        }

        // pitch を (-180, 180] に直してからクランプするのは、Unity の eulerAngles の 0〜360 で渡されても上下を取り違えないため。
        public void Set(float yaw, float pitch)
        {
            Yaw = NormalizeYaw(Finite(yaw));
            Pitch = ClampPitch(ToSigned(Finite(pitch)));
        }

        // 有限でない入力 (NaN など) を無視するのは、一度入ると向きが壊れたまま戻らないため。
        public void Rotate(float yawDelta, float pitchUpDelta)
        {
            Yaw = NormalizeYaw(Yaw + Finite(yawDelta));
            Pitch = ClampPitch(Pitch - Finite(pitchUpDelta));
        }

        public static float ToSigned(float degrees)
        {
            var wrapped = NormalizeYaw(degrees);
            return wrapped > 180f ? wrapped - 360f : wrapped;
        }

        private float ClampPitch(float pitch)
        {
            return Math.Max(-MaxPitch, Math.Min(MaxPitch, pitch));
        }

        private static float NormalizeYaw(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            // -1e-6 % 360 + 360 は float で 360 に丸まることがあるので、範囲の上端を閉じておく。
            return wrapped >= 360f ? 0f : wrapped;
        }

        private static float Finite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
