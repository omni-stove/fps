using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) で再生する、倒した相手のある時点の目の位置と体の向き。
    /// 向きは <see cref="DeathCamFraming"/> と同じく Unity の規則の yaw (度、+Z が 0 で +X へ回る向きが正)。
    /// </summary>
    public readonly struct KillCamPose : IEquatable<KillCamPose>
    {
        /// <summary>目の位置 (ワールド座標、m)。</summary>
        public Vector3 Eye { get; }

        /// <summary>体の向き (度)。[0, 360) に揃える。</summary>
        public float Yaw { get; }

        /// <exception cref="ArgumentException">eye か yaw が有限でない。</exception>
        public KillCamPose(Vector3 eye, float yaw)
        {
            if (!IsFinite(eye.X) || !IsFinite(eye.Y) || !IsFinite(eye.Z))
            {
                throw new ArgumentException("eye must be finite", nameof(eye));
            }

            if (!IsFinite(yaw))
            {
                throw new ArgumentException("yaw must be finite", nameof(yaw));
            }

            Eye = eye;
            Yaw = NormalizeYaw(yaw);
        }

        /// <summary>
        /// from と to の間の t (0 から 1 に収める) の姿勢。位置は線形に、向きは短い方へ回して補間する
        /// (359° と 1° の間で逆回りしないように)。
        /// </summary>
        public static KillCamPose Lerp(KillCamPose from, KillCamPose to, float t)
        {
            var clamped = float.IsNaN(t) ? 0f : Math.Max(0f, Math.Min(1f, t));
            var delta = to.Yaw - from.Yaw;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return new KillCamPose(Vector3.Lerp(from.Eye, to.Eye, clamped), from.Yaw + (delta * clamped));
        }

        public bool Equals(KillCamPose other)
        {
            return Eye.Equals(other.Eye) && Yaw.Equals(other.Yaw);
        }

        public override bool Equals(object obj)
        {
            return obj is KillCamPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Eye.GetHashCode() * 397) ^ Yaw.GetHashCode();
        }

        public static bool operator ==(KillCamPose left, KillCamPose right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KillCamPose left, KillCamPose right)
        {
            return !left.Equals(right);
        }

        private static float NormalizeYaw(float yaw)
        {
            var wrapped = yaw % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            // -1e-6 % 360 + 360 は float では 360 に丸まり、[0, 360) を外れる
            return wrapped >= 360f ? 0f : wrapped;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
