using System;
using System.Numerics;

namespace Fps.Core
{
    public static partial class VaultProbe
    {
        /// <summary>
        /// よじ登った後に体が縁の上に立つのに要る、手前の面からの奥行き (m)。体の直径に <see cref="WallGap"/> を足したもの。
        /// 縁の奥がこれより短い (薄い壁など) と、上に立てないのでよじ登らない (<see cref="VaultVerdict.TooNarrow"/>)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">bodyRadius が負か有限でないとき。</exception>
        public static float MantleFootingDepth(float bodyRadius)
        {
            ValidateRadius(bodyRadius);
            return (bodyRadius * 2f) + WallGap;
        }

        /// <summary>
        /// 崖のぼり (<see cref="Mantle.On"/>) の判定。sample の上端が minHeight (<see cref="MotorSpec.VaultMaxHeight"/>、これ以下は乗り越えの受け持ち) より高く
        /// maxHeight (<see cref="MotorSpec.MantleMaxHeight"/>) 以下で、縁の奥行きが <see cref="MantleFootingDepth"/> 以上なら <see cref="PlanMantlePath"/> で軌跡を作り、
        /// isPathClear (物理で軌跡の上を体が通れるか) が false なら <see cref="VaultVerdict.Blocked"/> にする (上がる間の頭上か、縁の上が塞がっている)。
        /// 理由は TooFar・NotAWall・NotFacing・TooLow・TooHigh・TooNarrow・Blocked の順に調べ、最初に当たったものを返す。通れば <see cref="VaultVerdict.Mantle"/>。
        /// sample の Depth は、手前の面から <see cref="MantleFootingDepth"/> より先まで測ったもの (それより厚ければ無限大でよい)。
        /// </summary>
        /// <param name="sample">前方への Ray で測った段の標本。</param>
        /// <param name="facing">向いている向き。</param>
        /// <param name="minHeight">これ以下の高さは乗り越えの受け持ちで、崖のぼりは TooLow にする。</param>
        /// <param name="maxHeight">これより高い段には登らない。</param>
        /// <param name="bodyRadius">体の半径。</param>
        /// <param name="isPathClear">軌跡の上を体が通れるかを物理で調べる関数。</param>
        /// <param name="speed">軌跡をたどる速さ (<see cref="MotorSpec.MantleSpeed"/>)。</param>
        /// <exception cref="ArgumentOutOfRangeException">sample・facing・高さ・bodyRadius・speed の値が不正なとき。</exception>
        /// <exception cref="ArgumentNullException">isPathClear が null のとき。</exception>
        public static VaultProbeResult EvaluateMantle(
            VaultObstacleSample sample,
            Vector3 facing,
            float minHeight,
            float maxHeight,
            float bodyRadius,
            float speed,
            Func<VaultPath, bool> isPathClear)
        {
            if (!IsFinite(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must be finite");
            }

            ValidateLength(minHeight, nameof(minHeight));
            ValidateLength(maxHeight, nameof(maxHeight));
            ValidateRadius(bodyRadius);
            ValidateSpeed(speed);
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
            if (!TryReachWall(sample, out var into, out var rejected))
            {
                return new VaultProbeResult(rejected, height, depth, Vector3.Zero, null);
            }

            var verdict = ClassifyMantle(into, facing, height, depth, minHeight, maxHeight, bodyRadius);
            if (verdict != VaultVerdict.Mantle)
            {
                return new VaultProbeResult(verdict, height, depth, into, null);
            }

            var path = PlanMantlePath(into, sample.Distance, height, bodyRadius, speed);
            verdict = isPathClear(path) ? VaultVerdict.Mantle : VaultVerdict.Blocked;
            return new VaultProbeResult(verdict, height, depth, into, path);
        }

        /// <summary>
        /// direction (段へ向かう水平の向き) にある、手前の面まで distance・上端 topHeight の段に、半径 bodyRadius の体でよじ登る軌跡を作る。
        /// 頂点は始点・面の手前で上端より <see cref="Clearance"/> 上まで上がりきった所・縁の上へ移りきった所・縁の上に下りた所の 4 つ。
        /// 縁の上では体の後ろの端が手前の面から <see cref="WallGap"/> 奥に来る。
        /// </summary>
        /// <exception cref="ArgumentException">direction に水平の成分が無いとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">距離・高さ・半径が負か有限でない、または speed が正の有限値でないとき。</exception>
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
            Vector3 into, Vector3 facing, float height, float depth, float minHeight, float maxHeight, float bodyRadius)
        {
            if (!Faces(facing, into))
            {
                return VaultVerdict.NotFacing;
            }

            // 境界ちょうどの高さは乗り越えの受け持ち (Evaluate は maxHeight + ε まで乗り越える) なので、ここでは低いとみなす。
            if (height <= minHeight + _boundaryEpsilon)
            {
                return VaultVerdict.TooLow;
            }

            if (height > maxHeight + _boundaryEpsilon)
            {
                return VaultVerdict.TooHigh;
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
