using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class BlindFireShot
    {
        public const string LogPrefix = "[Combat] blind-fire";

        public const float LowWallClearance = 0.15f;

        public const float LowWallNormalOffset = 0.1f;

        public const float HighWallSideMargin = 0.15f;

        // WeaponSpec.BlindFireSpreadDegrees の上限と揃える。
        public const float MaxSpreadDegrees = 90f;

        private const float _minLength = 1e-6f;

        // 覗くときの側 (CoverProbe.PeekOffset) と同じ決め方にし、覗いた側と撃つ側をずらさない。
        public static int EdgeSide(CoverEdge edges, float preferredSide)
        {
            var left = (edges & CoverEdge.Left) != 0;
            var right = (edges & CoverEdge.Right) != 0;
            if (left && right)
            {
                return preferredSide < 0f ? -1 : 1;
            }

            if (left)
            {
                return -1;
            }

            return right ? 1 : 0;
        }

        public static bool TryResolveOrigin(
            CoverHeight height,
            CoverEdge edges,
            float preferredSide,
            Vector3 normal,
            float coverTopY,
            float bodyRadius,
            Vector3 eye,
            out Vector3 origin)
        {
            if (!IsFinite(normal))
            {
                throw new ArgumentOutOfRangeException(nameof(normal), normal, "normal must be finite");
            }

            if (!IsFinite(coverTopY))
            {
                throw new ArgumentOutOfRangeException(nameof(coverTopY), coverTopY, "coverTopY must be finite");
            }

            if (!(bodyRadius >= 0f) || float.IsInfinity(bodyRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(bodyRadius), bodyRadius, "bodyRadius must be finite and not negative");
            }

            if (!IsFinite(eye))
            {
                throw new ArgumentOutOfRangeException(nameof(eye), eye, "eye must be finite");
            }

            var flatNormal = CoverProbe.Horizontal(normal);
            if (flatNormal == Vector3.Zero)
            {
                throw new ArgumentException("normal must have a horizontal component", nameof(normal));
            }

            switch (height)
            {
                case CoverHeight.Low:
                    origin = new Vector3(eye.X, coverTopY + LowWallClearance, eye.Z) + (flatNormal * LowWallNormalOffset);
                    return true;

                case CoverHeight.High:
                    var side = EdgeSide(edges, preferredSide);
                    if (side == 0)
                    {
                        origin = eye;
                        return false;
                    }

                    origin = eye + (CoverProbe.RightAlong(flatNormal) * (side * (bodyRadius + HighWallSideMargin)));
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(height), height, "height is not a defined CoverHeight");
            }
        }

        // 乱数を外から渡すのは、テストで固定できるようにするため。
        public static Vector3 Spread(Vector3 direction, float halfAngleDegrees, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (!IsFinite(direction) || direction.Length() < _minLength)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (!(halfAngleDegrees >= 0f) || halfAngleDegrees > MaxSpreadDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfAngleDegrees), halfAngleDegrees, "halfAngleDegrees must be between 0 and " + MaxSpreadDegrees);
            }

            var forward = Vector3.Normalize(direction);
            if (halfAngleDegrees == 0f)
            {
                return forward;
            }

            // 円錐の上で一様にするには、極角ではなくその cos を [cos(半角), 1] で一様に選ぶ (球帯の面積は高さに比例する)。
            var cosMax = Math.Cos(halfAngleDegrees * Math.PI / 180.0);
            var cosTheta = 1.0 - (random.NextDouble() * (1.0 - cosMax));
            var sinTheta = Math.Sqrt(Math.Max(0.0, 1.0 - (cosTheta * cosTheta)));
            var phi = random.NextDouble() * 2.0 * Math.PI;

            Perpendiculars(forward, out var u, out var v);
            var sideways = (u * (float)Math.Cos(phi)) + (v * (float)Math.Sin(phi));
            var result = (forward * (float)cosTheta) + (sideways * (float)sinTheta);
            return Vector3.Normalize(result);
        }

        // ネット対戦で持ち主の予測とサーバーの判定を揃えるために、乱数ではなく種で決める。
        // 武器のばらつきとは別の系列を使い、同じ種でも別の向きにする。
        public static Vector3 Spread(Vector3 direction, float halfAngleDegrees, int playerId, int shotIndex)
        {
            if (!IsFinite(direction) || direction.Length() < _minLength)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (!(halfAngleDegrees >= 0f) || halfAngleDegrees > MaxSpreadDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfAngleDegrees), halfAngleDegrees, "halfAngleDegrees must be between 0 and " + MaxSpreadDegrees);
            }

            return SpreadPattern.Direction(
                direction, halfAngleDegrees, playerId, shotIndex, SpreadPattern.BlindFirePolarStream, SpreadPattern.BlindFireAzimuthStream);
        }

        public static string FormatLog(float spreadDegrees)
        {
            return LogPrefix + " spread=" + spreadDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // 外積が 0 に近づかないよう、forward と平行になりにくい軸を補助に選ぶ。
        private static void Perpendiculars(Vector3 forward, out Vector3 u, out Vector3 v)
        {
            var helper = Math.Abs(forward.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            u = Vector3.Normalize(Vector3.Cross(helper, forward));
            v = Vector3.Cross(forward, u);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }
    }
}
