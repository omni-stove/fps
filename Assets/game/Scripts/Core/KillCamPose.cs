using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct KillCamPose : IEquatable<KillCamPose>
    {
        public Vector3 Eye { get; }

        public float Yaw { get; }

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

        // 向きを短い方へ回して補間するのは、359° と 1° の間で逆回りしないようにするため。
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
