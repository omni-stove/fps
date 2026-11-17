using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class DeathCamFraming
    {
        public const float DefaultHeight = 1.5f;

        // 相手と反対側へカメラを下げるのは、倒れた位置も画面に入れるため。
        public const float DefaultBackDistance = 2f;

        public const float DefaultFreeSpeed = 6f;

        // これより短い水平の距離は、向きが決まらないとみなす。
        private const float _minLengthSquared = 1e-8f;

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

        public static Vector3 ShoulderPosition(Vector3 eye, float yaw, Vector3 shoulderOffset)
        {
            var forward = YawForward(yaw);
            var right = new Vector3(forward.Z, 0f, -forward.X);
            return eye + (right * shoulderOffset.X) + (Vector3.UnitY * shoulderOffset.Y) + (forward * shoulderOffset.Z);
        }

        public static Vector3 FreeMoveDelta(Vector2 move, float yaw, float pitch, float speed, float deltaSeconds)
        {
            RequireFiniteNonNegative(speed, nameof(speed));
            RequireFiniteNonNegative(deltaSeconds, nameof(deltaSeconds));

            var lengthSquared = move.LengthSquared();
            if (!IsFinite(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                return Vector3.Zero;
            }

            if (lengthSquared > 1f)
            {
                move /= (float)Math.Sqrt(lengthSquared);
            }

            var flat = YawForward(yaw);
            var right = new Vector3(flat.Z, 0f, -flat.X);
            var direction = (Forward(yaw, pitch) * move.Y) + (right * move.X);
            return direction * (speed * deltaSeconds);
        }

        // Unity の Quaternion.Euler(0, yaw, 0) * Vector3.forward と同じ向きにそろえる。
        public static Vector3 YawForward(float yaw)
        {
            var radians = (IsFinite(yaw) ? yaw : 0f) * (float)Math.PI / 180f;
            return new Vector3((float)Math.Sin(radians), 0f, (float)Math.Cos(radians));
        }

        // Unity の Quaternion.Euler(pitch, yaw, 0) * Vector3.forward と同じ向きにそろえる。
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
