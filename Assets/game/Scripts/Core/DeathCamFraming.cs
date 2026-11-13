using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// デスカム (<see cref="DeathCamera.Killer"/>) のカメラの置き場所と向きの計算。
    /// カメラは倒れた目の位置から、相手と反対側へ少し下がって上へ持ち上げた所 (<see cref="BoomOffset"/>) に置き、
    /// そこから相手 (いなければ倒れた目の位置) を向く (<see cref="LookAt"/>)。
    /// 角度は <see cref="LookAngles"/> と同じく Unity の規則で、yaw は +Z を 0 として +X へ回る向きが正、pitch は正が下向き。
    /// </summary>
    public static class DeathCamFraming
    {
        /// <summary>倒れた目からカメラを持ち上げる高さ (m)。</summary>
        public const float DefaultHeight = 1.5f;

        /// <summary>倒れた目から、相手と反対側へカメラを下げる水平の距離 (m)。倒れた位置も画面に入れるため。</summary>
        public const float DefaultBackDistance = 2f;

        // これより短い水平の距離は、向きが決まらないとみなす。
        private const float _minLengthSquared = 1e-8f;

        /// <summary>
        /// 倒れた目からカメラへのオフセット (ワールド座標、m)。上へ height、target と反対の水平方向へ backDistance。
        /// target が真上・真下・同じ位置で水平の向きが決まらないときは、fallbackYaw の向きの反対へ下げる。
        /// </summary>
        /// <param name="deathEye">倒れたときの目の位置。</param>
        /// <param name="target">映す相手の位置。相手がいなければ deathEye を渡す。</param>
        /// <param name="fallbackYaw">向きが決まらないときに正面とみなす yaw (度)。普通は倒れたときの体の向き。</param>
        /// <param name="height">持ち上げる高さ (m)。0 以上の有限値。</param>
        /// <param name="backDistance">下げる水平の距離 (m)。0 以上の有限値。</param>
        public static Vector3 BoomOffset(Vector3 deathEye, Vector3 target, float fallbackYaw, float height, float backDistance)
        {
            RequireFiniteNonNegative(height, nameof(height));
            RequireFiniteNonNegative(backDistance, nameof(backDistance));

            var flat = new Vector3(target.X - deathEye.X, 0f, target.Z - deathEye.Z);
            var lengthSquared = flat.LengthSquared();
            Vector3 forward;
            if (IsFinite(lengthSquared) && lengthSquared >= _minLengthSquared)
            {
                forward = flat / (float)Math.Sqrt(lengthSquared);
            }
            else
            {
                forward = YawForward(fallbackYaw);
            }

            return (Vector3.UnitY * height) - (forward * backDistance);
        }

        /// <summary>
        /// from から target を向く角度 (度)。yaw は [0, 360)、pitch は ±maxPitch に収める (<see cref="LookAngles.Set"/>)。
        /// target が真上・真下で yaw が決まらなければ fallbackYaw のまま上か下を向き、
        /// 同じ位置か有限でなく向きがまったく決まらなければ fallbackYaw のまま真下 (maxPitch) を見下ろす。
        /// </summary>
        /// <param name="from">カメラの位置。</param>
        /// <param name="target">向く先。</param>
        /// <param name="fallbackYaw">yaw が決まらないときの yaw (度)。</param>
        /// <param name="maxPitch">pitch の上限 (度)。0 より大きく 90 以下。</param>
        /// <param name="yaw">左右の向き (度)。</param>
        /// <param name="pitch">上下の向き (度、正が下)。</param>
        public static void LookAt(Vector3 from, Vector3 target, float fallbackYaw, float maxPitch, out float yaw, out float pitch)
        {
            var angles = new LookAngles(maxPitch);
            var delta = target - from;
            var flatSquared = (delta.X * delta.X) + (delta.Z * delta.Z);
            if (!IsFinite(flatSquared) || !IsFinite(delta.Y))
            {
                angles.Set(fallbackYaw, maxPitch);
            }
            else if (flatSquared < _minLengthSquared)
            {
                // 真上を向くのは相手が真上にいるときだけで、重なっていれば倒れた所を見下ろす。
                angles.Set(fallbackYaw, delta.Y > 0f ? -maxPitch : maxPitch);
            }
            else
            {
                var degreesPerRadian = 180f / (float)Math.PI;
                var flat = (float)Math.Sqrt(flatSquared);
                angles.Set(
                    (float)Math.Atan2(delta.X, delta.Z) * degreesPerRadian,
                    -(float)Math.Atan2(delta.Y, flat) * degreesPerRadian);
            }

            yaw = angles.Yaw;
            pitch = angles.Pitch;
        }

        /// <summary>yaw (度) の水平の正面。Unity の Quaternion.Euler(0, yaw, 0) * Vector3.forward と同じ向き。</summary>
        public static Vector3 YawForward(float yaw)
        {
            var radians = (IsFinite(yaw) ? yaw : 0f) * (float)Math.PI / 180f;
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        /// <summary>yaw と pitch (度) の正面。Unity の Quaternion.Euler(pitch, yaw, 0) * Vector3.forward と同じ向き。</summary>
        public static Vector3 Forward(float yaw, float pitch)
        {
            var pitchRadians = (IsFinite(pitch) ? pitch : 0f) * (float)Math.PI / 180f;
            var flat = YawForward(yaw) * (float)Math.Cos(pitchRadians);
            return new Vector3(flat.X, -(float)Math.Sin(pitchRadians), flat.Z);
        }

        private static void RequireFiniteNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
