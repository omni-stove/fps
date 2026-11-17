using System;
using System.Numerics;

namespace Shooter.Core
{
    public static partial class VaultProbe
    {
        public static float MantleFootingDepth(float bodyRadius)
        {
            ValidateRadius(bodyRadius);
            return (bodyRadius * 2f) + WallGap;
        }

        public static VaultProbeResult EvaluateMantle(
            VaultObstacleSample sample,
            Vector3 facing,
            MotorSpec spec,
            float bodyRadius,
            Func<VaultPath, bool> isPathClear)
        {
            if (!IsFinite(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must be finite");
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            ValidateRadius(bodyRadius);
            if (isPathClear == null)
            {
                throw new ArgumentNullException(nameof(isPathClear));
            }

            if (!sample.HasHit)
            {
                return VaultProbeResult.None;
            }

            ValidateSample(sample);
            var height = sample.TopHeight;
            var depth = sample.Depth;
            if (!TryReachWall(sample, spec.VaultReach, out var into, out var rejected))
            {
                return new VaultProbeResult(rejected, height, depth, Vector3.Zero, null);
            }

            var verdict = ClassifyMantle(into, facing, height, depth, spec, bodyRadius, sample.Width);
            if (verdict != VaultVerdict.Mantle)
            {
                return new VaultProbeResult(verdict, height, depth, into, null);
            }

            var path = PlanMantlePath(into, sample.Distance, height, bodyRadius, spec.MantleSpeed);
            verdict = isPathClear(path) ? VaultVerdict.Mantle : VaultVerdict.Blocked;
            return new VaultProbeResult(verdict, height, depth, into, path);
        }

        public static VaultPath PlanMantlePath(Vector3 direction, float distance, float topHeight, float bodyRadius, float speed)
        {
            var into = CoverProbe.Horizontal(direction);
            if (!IsFinite(direction) || into == Vector3.Zero)
            {
                throw new ArgumentException("direction must be finite and have a horizontal component", nameof(direction));
            }

            ValidateLength(distance, nameof(distance));
            ValidateLength(topHeight, nameof(topHeight));
            ValidateRadius(bodyRadius);
            ValidateSpeed(speed);

            // 乗り越えと同じく、寄るのは体の表面が面の WallGap 手前に来るまで。上がりながら寄っても面には触れない。
            var approach = Math.Max(0f, distance - bodyRadius - WallGap);
            var landing = distance + bodyRadius + WallGap;
            var up = Vector3.UnitY * (topHeight + Clearance);
            var points = new[]
            {
                Vector3.Zero,
                (into * approach) + up,
                (into * landing) + up,
                (into * landing) + (Vector3.UnitY * topHeight),
            };
            return new VaultPath(points, speed);
        }

        private static VaultVerdict ClassifyMantle(
            Vector3 into, Vector3 facing, float height, float depth, MotorSpec spec, float bodyRadius, float width)
        {
            if (!Faces(facing, into, spec.VaultMaxAngleDegrees))
            {
                return VaultVerdict.NotFacing;
            }

            // 境界ちょうどの高さは乗り越えの受け持ち (Evaluate は maxHeight + ε まで乗り越える) なので、ここでは低いとみなす。
            if (height <= spec.VaultMaxHeight + _boundaryEpsilon)
            {
                return VaultVerdict.TooLow;
            }

            if (height > spec.MantleMaxHeight + _boundaryEpsilon)
            {
                return VaultVerdict.TooHigh;
            }

            if (width < MinFaceWidth(bodyRadius) - _boundaryEpsilon)
            {
                return VaultVerdict.NarrowFace;
            }

            return depth < MantleFootingDepth(bodyRadius) - _boundaryEpsilon ? VaultVerdict.TooNarrow : VaultVerdict.Mantle;
        }

        private static void ValidateSpeed(float speed)
        {
            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite and positive");
            }
        }
    }
}
