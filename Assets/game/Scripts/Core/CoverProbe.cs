using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class CoverProbe
    {
        public const float EdgeProbeDistance = 0.3f;

        public const float WallGap = 0.05f;

        public const float MaxWallNormalUp = 0.3f;

        // 角度と距離の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        private const float _minLength = 1e-6f;

        public static CoverProbeResult Evaluate(CoverWallSample sample, Vector3 facing, MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!sample.HasHit)
            {
                return CoverProbeResult.None;
            }

            if (!IsFinite(sample.Distance) || !IsFinite(sample.TopHeight) || !IsFinite(sample.Normal))
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample must be finite");
            }

            if (sample.Distance < 0f || sample.Distance > spec.CoverMaxEnterDistance + _boundaryEpsilon)
            {
                return CoverProbeResult.None;
            }

            var normalLength = sample.Normal.Length();
            if (normalLength < _minLength || Math.Abs(sample.Normal.Y / normalLength) > MaxWallNormalUp)
            {
                return CoverProbeResult.None;
            }

            if (sample.TopHeight < spec.CoverMinWallHeight - _boundaryEpsilon)
            {
                return CoverProbeResult.None;
            }

            var normal = Horizontal(sample.Normal);
            var height = sample.TopHeight < spec.CoverLowWallHeight ? CoverHeight.Low : CoverHeight.High;
            var edges = CoverEdge.None;
            if (!sample.WallOnLeft)
            {
                edges |= CoverEdge.Left;
            }

            if (!sample.WallOnRight)
            {
                edges |= CoverEdge.Right;
            }

            return new CoverProbeResult(true, FacesWall(facing, normal, spec.CoverMaxEnterAngleDegrees), height, edges, normal, sample.Distance, sample.TopHeight);
        }

        public static float PeekOffset(CoverProbeResult result, float preferredSide, MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var peekSideDistance = spec.CoverPeekSideDistance;
            if (!result.IsWall || result.Height == CoverHeight.Low)
            {
                return 0f;
            }

            if (result.IsAtLeftEdge && result.IsAtRightEdge)
            {
                return preferredSide < 0f ? -peekSideDistance : peekSideDistance;
            }

            if (result.IsAtLeftEdge)
            {
                return -peekSideDistance;
            }

            return result.IsAtRightEdge ? peekSideDistance : 0f;
        }

        // 寄せる間は体から法線の逆向きに測り直すので、斜めから入ると当たり点から横へずれた所を測ってしまう。
        // このずれを AppliedPeek (測る位置から体が右へ出ている量) として持てば、測り直しは当たり点の正面から撃てる。
        public static float EnterSideOffset(Vector3 facing, Vector3 normal, float distance)
        {
            var forward = Horizontal(facing);
            if (forward == Vector3.Zero || !IsFinite(distance))
            {
                return 0f;
            }

            return -Vector3.Dot(forward * distance, RightAlong(normal));
        }

        public static float HoldDistance(float bodyRadius)
        {
            if (!(bodyRadius >= 0f) || float.IsInfinity(bodyRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(bodyRadius), bodyRadius, "bodyRadius must be finite and not negative");
            }

            return bodyRadius + WallGap;
        }

        public static Vector3 RightAlong(Vector3 normal)
        {
            var into = -Horizontal(normal);
            if (into == Vector3.Zero)
            {
                throw new ArgumentException("normal must have a horizontal component", nameof(normal));
            }

            // Y が上・Z が前の並びでは Cross(上, 前) が右になる (Unity の左手系と同じ数値)。
            return Vector3.Normalize(Vector3.Cross(Vector3.UnitY, into));
        }

        public static Vector3 Horizontal(Vector3 v)
        {
            var flat = new Vector3(v.X, 0f, v.Z);
            var length = flat.Length();
            return length < _minLength ? Vector3.Zero : flat / length;
        }

        private static bool FacesWall(Vector3 facing, Vector3 normal, float maxAngleDegrees)
        {
            var forward = Horizontal(facing);
            if (forward == Vector3.Zero)
            {
                return false;
            }

            var cos = Vector3.Dot(forward, -normal);
            var limit = (float)Math.Cos(maxAngleDegrees * Math.PI / 180.0);
            return cos >= limit - _boundaryEpsilon;
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
