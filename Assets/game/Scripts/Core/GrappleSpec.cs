using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class GrappleSpec
    {
        public const float DefaultMinRange = 2f;
        public const float DefaultMaxRange = 30f;
        public const float DefaultPullSpeed = 18f;
        public const float DefaultPullAcceleration = 60f;
        public const float DefaultArriveDistance = 0.3f;

        // 途中で物に引っかかって着かないまま引き続けないよう、時間で打ち切る。
        public const float DefaultMaxPullSeconds = 3f;

        // 立ち 2 m のプレイヤーで縁から足元までが 1.8 m になり、崖のぼりの届く高さ (MotorSpec.DefaultMantleMaxHeight) に収まる。
        public const float DefaultHangHeight = 0.8f;

        // 壁の上の方を狙っても縁にぴったり当てるのは難しいので、縁の近くに刺さったら縁へ寄せる。
        public const float DefaultEdgeSnapDistance = 1.5f;

        public const float DefaultHangSeconds = 0.5f;

        // CharacterController の登れる角度の既定 (45°) より少し緩くし、坂に刺さっても上に立たせる。
        public const float FloorNormalY = 0.7f;

        // CharacterController の skinWidth で面から押し返されても、着いたかの判定 (ArriveDistance) に収まるだけ空ける。
        public const float SurfaceGap = 0.05f;

        public static readonly GrappleSpec Default = new GrappleSpec();

        public GrappleSpec(
            float minRange = DefaultMinRange,
            float maxRange = DefaultMaxRange,
            float pullSpeed = DefaultPullSpeed,
            float pullAcceleration = DefaultPullAcceleration,
            float arriveDistance = DefaultArriveDistance,
            float maxPullSeconds = DefaultMaxPullSeconds,
            float hangHeight = DefaultHangHeight,
            float edgeSnapDistance = DefaultEdgeSnapDistance,
            float hangSeconds = DefaultHangSeconds)
        {
            RequireNonNegativeFinite(minRange, nameof(minRange));
            RequirePositiveFinite(maxRange, nameof(maxRange));
            RequirePositiveFinite(pullSpeed, nameof(pullSpeed));
            RequirePositiveFinite(pullAcceleration, nameof(pullAcceleration));
            RequirePositiveFinite(arriveDistance, nameof(arriveDistance));
            RequirePositiveFinite(maxPullSeconds, nameof(maxPullSeconds));
            RequireNonNegativeFinite(hangHeight, nameof(hangHeight));
            RequireNonNegativeFinite(edgeSnapDistance, nameof(edgeSnapDistance));
            RequireNonNegativeFinite(hangSeconds, nameof(hangSeconds));
            if (minRange >= maxRange)
            {
                throw new ArgumentOutOfRangeException(nameof(minRange), minRange, "minRange must be less than maxRange");
            }

            MinRange = minRange;
            MaxRange = maxRange;
            PullSpeed = pullSpeed;
            PullAcceleration = pullAcceleration;
            ArriveDistance = arriveDistance;
            MaxPullSeconds = maxPullSeconds;
            HangHeight = hangHeight;
            EdgeSnapDistance = edgeSnapDistance;
            HangSeconds = hangSeconds;
        }

        public float MinRange { get; }

        public float MaxRange { get; }

        public float PullSpeed { get; }

        public float PullAcceleration { get; }

        public float ArriveDistance { get; }

        public float MaxPullSeconds { get; }

        public float HangHeight { get; }

        public float EdgeSnapDistance { get; }

        public float HangSeconds { get; }

        public bool InRange(float distance)
        {
            return distance >= MinRange && distance <= MaxRange;
        }

        // 床なら上に立ち、天井ならその下に頭を付け、壁なら面の手前で手 (中心から HangHeight 上) が anchor に来る所。
        public Vector3 HangPoint(Vector3 anchor, Vector3 normal, float bodyRadius, float bodyHalfHeight)
        {
            RequireFinite(anchor, nameof(anchor));
            RequireFinite(normal, nameof(normal));
            RequireNonNegativeFinite(bodyRadius, nameof(bodyRadius));
            RequireNonNegativeFinite(bodyHalfHeight, nameof(bodyHalfHeight));
            if (normal.LengthSquared() < 1e-8f)
            {
                throw new ArgumentException("normal must not be zero", nameof(normal));
            }

            normal = Vector3.Normalize(normal);
            if (normal.Y >= FloorNormalY)
            {
                return anchor + (Vector3.UnitY * (bodyHalfHeight + SurfaceGap));
            }

            if (!IsWall(normal))
            {
                return anchor - (Vector3.UnitY * (bodyHalfHeight + SurfaceGap));
            }

            var flat = Vector3.Normalize(new Vector3(normal.X, 0f, normal.Z));
            return anchor + (flat * (bodyRadius + SurfaceGap)) - (Vector3.UnitY * HangHeight);
        }

        public static bool IsWall(Vector3 normal)
        {
            if (!(normal.LengthSquared() >= 1e-8f))
            {
                return false;
            }

            var y = Vector3.Normalize(normal).Y;
            return y < FloorNormalY && y > -FloorNormalY;
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentException($"{name} must be finite: {value}", name);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a positive finite number");
            }
        }
    }
}
