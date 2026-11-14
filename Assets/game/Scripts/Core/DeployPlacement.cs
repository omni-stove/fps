using System;
using System.Numerics;

namespace Fps.Core
{
    public static class DeployPlacement
    {
        // 理由が重なったら、プレイヤーが直しやすい順 (狙いを変える → 場所を変える → 物をどける) の先のものを返す。
        public static DeployVerdict Judge(DeploySpec spec, DeploySurvey survey)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!survey.HasGround)
            {
                return DeployVerdict.NoGround;
            }

            if (SlopeDegrees(survey.GroundNormal) > spec.MaxSlopeDegrees)
            {
                return DeployVerdict.TooSteep;
            }

            if (!survey.AllCornersGrounded
                || -survey.LowestCorner > spec.MaxStepHeight
                || survey.HighestCorner > spec.MaxStepHeight)
            {
                return DeployVerdict.Uneven;
            }

            return survey.Blocked ? DeployVerdict.Blocked : DeployVerdict.Ok;
        }

        public static float SlopeDegrees(Vector3 normal)
        {
            var length = normal.Length();
            if (!(length > 1e-6f) || float.IsInfinity(length))
            {
                throw new ArgumentOutOfRangeException(nameof(normal), normal, "normal must be non-zero and finite");
            }

            var cos = Math.Max(-1f, Math.Min(1f, normal.Y / length));
            return (float)(Math.Acos(cos) * (180d / Math.PI));
        }

        // 陣営が分からない相手を味方と決めると敵の弾まで通すおそれがあるので、分からなければ持ち主の弾だけを通す。
        public static bool LetsThrough(bool shooterIsOwner, Side? deployedSide, Side? shooterSide)
        {
            if (shooterIsOwner)
            {
                return true;
            }

            return deployedSide.HasValue && shooterSide.HasValue && deployedSide.Value == shooterSide.Value;
        }
    }
}
