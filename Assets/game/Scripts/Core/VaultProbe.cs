using System;
using System.Numerics;

namespace Shooter.Core
{
    public static partial class VaultProbe
    {
        // 既定の MotorSpec.DefaultVaultMinHeight より上で、低い障害物にも当たる高さ。
        public const float LowestProbeHeight = 0.35f;

        // 台やアーチのように下が抜けて上が張り出した物は、低い Ray が下をくぐるので、張り出しの縁に当てるために上にも並べる。
        public const float ProbeHeightStep = 0.1f;

        public const int MaxProbeHeights = 32;

        public const float FaceWidthStep = 0.1f;

        public const float FaceWidthBack = 0.1f;

        // 少し斜めの面や丸い面でも端まで続いているとみなすため、FaceWidthBack よりこれだけ奥で当たっても同じ面とみなす。
        public const float FaceWidthSlack = 0.1f;

        public const int MaxFaceWidthProbes = 32;

        public const float Clearance = 0.15f;

        // CharacterController の既定の skinWidth (0.08) より広くし、寄る途中で面に押し返されて軌跡からずれないようにする。
        public const float WallGap = 0.1f;

        // 角度と距離の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        public static VaultProbeResult Evaluate(
            VaultObstacleSample sample, Vector3 facing, MotorSpec spec, float bodyRadius, Func<VaultPath, bool> isPathClear)
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

            var verdict = Classify(into, facing, height, depth, spec, sample.Width, bodyRadius);
            var onto = verdict == VaultVerdict.TooDeep && depth >= MantleFootingDepth(bodyRadius) - _boundaryEpsilon;
            if (verdict != VaultVerdict.Vault && !onto)
            {
                return new VaultProbeResult(verdict, height, depth, into, null);
            }

            // 崖のぼりは乗り越えられる高さを受け持たないので、ここで乗せないと、ジャンプの無いルールでは腰の高さの台に上がれない
            var path = onto
                ? PlanMantlePath(into, sample.Distance, height, bodyRadius, spec.VaultSpeed)
                : PlanPath(into, sample.Distance, height, depth, bodyRadius, spec.VaultSpeed);
            verdict = isPathClear(path) ? VaultVerdict.Vault : VaultVerdict.Blocked;
            return new VaultProbeResult(verdict, height, depth, into, path);
        }

        public static VaultPath PlanPath(Vector3 direction, float distance, float topHeight, float depth, float bodyRadius, float speed)
        {
            var into = CoverProbe.Horizontal(direction);
            if (!IsFinite(direction) || into == Vector3.Zero)
            {
                throw new ArgumentException("direction must be finite and have a horizontal component", nameof(direction));
            }

            ValidateLength(distance, nameof(distance));
            ValidateLength(topHeight, nameof(topHeight));
            ValidateLength(depth, nameof(depth));
            ValidateRadius(bodyRadius);
            ValidateSpeed(speed);

            // 手前へ寄るのは体の表面が面の WallGap 手前に来るまで。すでにそれより近ければ寄らずに真上へ上がる。
            var approach = Math.Max(0f, distance - bodyRadius - WallGap);
            var landing = distance + depth + bodyRadius + WallGap;
            var up = Vector3.UnitY * (topHeight + Clearance);
            var points = new[]
            {
                Vector3.Zero,
                (into * approach) + up,
                (into * landing) + up,
                into * landing,
            };
            return new VaultPath(points, speed);
        }

        public static int ProbeHeightCount(float maxHeight)
        {
            ValidateLength(maxHeight, nameof(maxHeight));
            var above = maxHeight - LowestProbeHeight - _boundaryEpsilon;
            if (above <= 0f)
            {
                return 1;
            }

            return Math.Min(MaxProbeHeights, 1 + (int)Math.Floor(above / ProbeHeightStep));
        }

        public static float ProbeHeightAt(int index)
        {
            if (index < 0 || index >= MaxProbeHeights)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be from 0 to MaxProbeHeights - 1");
            }

            return LowestProbeHeight + (index * ProbeHeightStep);
        }

        public static bool IsWall(Vector3 normal)
        {
            if (!IsFinite(normal))
            {
                return false;
            }

            var normalLength = normal.Length();
            return normalLength > 0f
                && Math.Abs(normal.Y / normalLength) <= CoverProbe.MaxWallNormalUp
                && CoverProbe.Horizontal(normal) != Vector3.Zero;
        }

        public static bool IsBetterFace(bool hasBest, Vector3 bestNormal, float bestDistance, Vector3 candidateNormal, float candidateDistance)
        {
            if (!hasBest)
            {
                return true;
            }

            var bestWall = IsWall(bestNormal);
            var candidateWall = IsWall(candidateNormal);
            if (bestWall != candidateWall)
            {
                return candidateWall;
            }

            return candidateDistance < bestDistance;
        }

        public static float MinFaceWidth(float bodyRadius)
        {
            ValidateRadius(bodyRadius);
            return bodyRadius * 2f;
        }

        public static int FaceWidthProbeCount(float bodyRadius)
        {
            ValidateRadius(bodyRadius);
            return Math.Min(MaxFaceWidthProbes, (int)Math.Ceiling((bodyRadius / FaceWidthStep) - _boundaryEpsilon));
        }

        public static float FaceWidthOffset(float bodyRadius, int index)
        {
            var count = FaceWidthProbeCount(bodyRadius);
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be from 0 to FaceWidthProbeCount - 1");
            }

            return Math.Min((index + 1) * FaceWidthStep, bodyRadius);
        }

        public static float MeasureFaceWidth(Vector3 point, Vector3 into, float bodyRadius, FaceRaycast raycast)
        {
            if (raycast == null)
            {
                throw new ArgumentNullException(nameof(raycast));
            }

            var forward = CoverProbe.Horizontal(into);
            if (!IsFinite(into) || forward == Vector3.Zero)
            {
                throw new ArgumentException("into must be finite and have a horizontal component", nameof(into));
            }

            var side = new Vector3(-forward.Z, 0f, forward.X);
            var count = FaceWidthProbeCount(bodyRadius);
            var width = 0f;
            for (var sign = -1; sign <= 1; sign += 2)
            {
                var extent = 0f;
                for (var i = 0; i < count; i++)
                {
                    var offset = FaceWidthOffset(bodyRadius, i);
                    var origin = point - (forward * FaceWidthBack) + (side * (sign * offset));
                    if (!raycast(origin, forward, FaceWidthBack + FaceWidthSlack, out var normal, out _) || !IsWall(normal))
                    {
                        break;
                    }

                    extent = offset;
                }

                width += extent;
            }

            return width;
        }

        public delegate bool FaceRaycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 normal, out float distance);

        private static VaultVerdict Classify(
            Vector3 into, Vector3 facing, float height, float depth, MotorSpec spec, float width, float bodyRadius)
        {
            if (!Faces(facing, into, spec.VaultMaxAngleDegrees))
            {
                return VaultVerdict.NotFacing;
            }

            if (height < spec.VaultMinHeight)
            {
                return VaultVerdict.TooLow;
            }

            if (height > spec.VaultMaxHeight + _boundaryEpsilon)
            {
                return VaultVerdict.TooHigh;
            }

            if (width < MinFaceWidth(bodyRadius) - _boundaryEpsilon)
            {
                return VaultVerdict.NarrowFace;
            }

            return depth > spec.VaultMaxDepth + _boundaryEpsilon ? VaultVerdict.TooDeep : VaultVerdict.Vault;
        }

        private static void ValidateSample(VaultObstacleSample sample)
        {
            if (!IsFinite(sample.Distance) || !IsFinite(sample.TopHeight) || !IsFinite(sample.Normal) || float.IsNaN(sample.Depth))
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample must be finite (Depth may be positive infinity)");
            }

            if (sample.Distance < 0f || sample.Depth < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample distance and depth must not be negative");
            }

            if (!(sample.Width >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample width must not be negative (it may be positive infinity)");
            }
        }

        private static bool TryReachWall(VaultObstacleSample sample, float reach, out Vector3 into, out VaultVerdict rejected)
        {
            into = Vector3.Zero;
            if (sample.Distance > reach + _boundaryEpsilon)
            {
                rejected = VaultVerdict.TooFar;
                return false;
            }

            var horizontal = -CoverProbe.Horizontal(sample.Normal);
            if (!IsWall(sample.Normal))
            {
                rejected = VaultVerdict.NotAWall;
                return false;
            }

            into = horizontal;
            rejected = VaultVerdict.NoObstacle;
            return true;
        }

        private static bool Faces(Vector3 facing, Vector3 into, float maxAngleDegrees)
        {
            var forward = CoverProbe.Horizontal(facing);
            if (forward == Vector3.Zero)
            {
                return false;
            }

            var limit = (float)Math.Cos(maxAngleDegrees * Math.PI / 180.0);
            return Vector3.Dot(forward, into) >= limit - _boundaryEpsilon;
        }

        private static void ValidateRadius(float bodyRadius)
        {
            ValidateLength(bodyRadius, nameof(bodyRadius));
        }

        private static void ValidateLength(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and not negative");
            }
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
