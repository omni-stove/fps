using System;

namespace Fps.Core
{
    /// <summary>
    /// 一人称視点の向き。yaw (左右, 度) は [0, 360) に正規化し、pitch (上下, 度) は ±MaxPitch に収める。
    /// pitch は Unity の X 軸回転と同じく正が下向き。
    /// </summary>
    public sealed class LookAngles
    {
        /// <summary>真上・真下を向いてジンバルが乱れないよう、既定では ±85° で止める。</summary>
        public const float DefaultMaxPitch = 85f;

        public float MaxPitch { get; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        /// <param name="maxPitch">pitch の上限 (度)。0 より大きく 90 以下。</param>
        public LookAngles(float maxPitch = DefaultMaxPitch)
        {
            if (!(maxPitch > 0f && maxPitch <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxPitch), maxPitch, "maxPitch must be in (0, 90]");
            }

            MaxPitch = maxPitch;
        }

        /// <summary>
        /// 向きを直接設定する。角度はどの範囲で渡してもよく (例: Unity の eulerAngles の 0〜360)、
        /// yaw は正規化、pitch は (-180, 180] に直してからクランプする。有限でない値は 0 として扱う。
        /// </summary>
        public void Set(float yaw, float pitch)
        {
            Yaw = NormalizeYaw(Finite(yaw));
            Pitch = ClampPitch(ToSigned(Finite(pitch)));
        }

        /// <summary>
        /// 入力分だけ回す。yawDelta は正で右、pitchUpDelta は正で上を向く。
        /// 有限でない入力 (NaN など) は無視し、向きが壊れないようにする。
        /// </summary>
        public void Rotate(float yawDelta, float pitchUpDelta)
        {
            Yaw = NormalizeYaw(Yaw + Finite(yawDelta));
            Pitch = ClampPitch(Pitch - Finite(pitchUpDelta));
        }

        /// <summary>角度を (-180, 180] に直す。</summary>
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
