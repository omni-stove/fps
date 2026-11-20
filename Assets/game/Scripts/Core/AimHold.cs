using System;
using System.Numerics;

namespace Shooter.Core
{
    // 三人称の覗き込みでカメラが肩から目へ寄るとき、向きをそのままにすると画面の中央が肩の幅だけ平行にずれ、
    // 照準の下にあった物が中央から外れる。寄せた後のカメラから同じ点が中央に来るよう、視点を回す角度を求める。
    // 角度は Unity の向き (y が上・z が前の左手系) で、x が右へのヨー、y が上へのピッチ (度)。
    public static class AimHold
    {
        // 近い点ほど寄せる間の回りが大きくなり、壁際で覗き込むと視点が大きく振れる。目からこれより近い点は、この距離にあるものとして保つ。
        public const float DefaultMinDistance = 2f;

        private const float _minLengthSquared = 1e-12f;

        private const float _radiansToDegrees = (float)(180.0 / Math.PI);

        // 距離をカメラでなく目から測るのは、視点は目を軸に回るので、目より近い点を保とうとすると回すほど点から離れていくため。
        public static Vector3 HeldPoint(Vector3 cameraPosition, Vector3 cameraForward, Vector3 eye, Vector3 aimPoint, float minDistance)
        {
            if (!(minDistance >= 0f) || float.IsInfinity(minDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(minDistance), minDistance, "minDistance must be a non-negative finite number");
            }

            var forward = NormalizeOrThrow(cameraForward, nameof(cameraForward));
            var nearest = AimSolver.RayStart(cameraPosition, forward, eye) + minDistance;
            var along = Vector3.Dot(aimPoint - cameraPosition, forward);
            return along >= nearest ? aimPoint : cameraPosition + (forward * nearest);
        }

        public static Vector2 LookDelta(Vector3 cameraForward, Vector3 movedCameraPosition, Vector3 heldPoint)
        {
            var forward = NormalizeOrThrow(cameraForward, nameof(cameraForward));
            var toPoint = heldPoint - movedCameraPosition;
            var lengthSquared = toPoint.LengthSquared();
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                return Vector2.Zero;
            }

            var desired = toPoint / (float)Math.Sqrt(lengthSquared);
            var yaw = WrapDegrees(Yaw(desired) - Yaw(forward));
            var pitch = PitchUp(desired) - PitchUp(forward);
            return new Vector2(yaw, pitch);
        }

        private static float Yaw(Vector3 direction)
        {
            return (float)Math.Atan2(direction.X, direction.Z) * _radiansToDegrees;
        }

        private static float PitchUp(Vector3 direction)
        {
            return (float)Math.Asin(Math.Max(-1f, Math.Min(1f, direction.Y))) * _radiansToDegrees;
        }

        private static float WrapDegrees(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped > 180f)
            {
                return wrapped - 360f;
            }

            return wrapped < -180f ? wrapped + 360f : wrapped;
        }

        private static Vector3 NormalizeOrThrow(Vector3 value, string name)
        {
            var lengthSquared = value.LengthSquared();
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < _minLengthSquared)
            {
                throw new ArgumentException(name + " must be a finite non-zero vector", name);
            }

            return value / (float)Math.Sqrt(lengthSquared);
        }
    }
}
