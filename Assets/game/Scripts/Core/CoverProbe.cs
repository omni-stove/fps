using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 壁に張り付くカバーの判定。物理で調べた壁の様子 (<see cref="CoverWallSample"/>) から、
    /// 入れるか・低い壁か高い壁か・左右の端にいるか・覗くときの横の移動量を決める。座標は Y が上、左右は壁に向いたときの左右。
    /// </summary>
    public static class CoverProbe
    {
        /// <summary>カバーに入れる、体の中心から壁までの水平距離の上限 (m)。</summary>
        public const float MaxEnterDistance = 1.0f;

        /// <summary>向いている方向と壁へ向かう方向 (法線の逆) のなす角の上限 (度)。</summary>
        public const float MaxEnterAngleDegrees = 45f;

        /// <summary>上端がこの高さ (足元から、m) 未満の壁を低い壁とする。</summary>
        public const float LowWallHeight = 1.1f;

        /// <summary>端の検査で、壁沿いに左右へずらす距離 (m)。</summary>
        public const float EdgeProbeDistance = 0.3f;

        /// <summary>高い壁の端から覗くときに横へ出る距離 (m)。</summary>
        public const float PeekSideDistance = 0.6f;

        /// <summary>張り付いたときの、体の表面と壁の隙間 (m)。</summary>
        public const float WallGap = 0.05f;

        /// <summary>
        /// 法線の上下の成分 (長さ 1 にしたとき) の絶対値の上限。これを超える面は床・天井・急な斜面で、壁として扱わない。
        /// 0.3 は鉛直から約 17° までの傾きを壁と見なす。
        /// </summary>
        public const float MaxWallNormalUp = 0.3f;

        // 角度と距離の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        private const float _minLength = 1e-6f;

        /// <summary>
        /// sample を判定する。facing は向いている方向 (上下の成分は無視する)。
        /// カバー中は壁へ向かう方向 (法線の逆) を渡せば、角度の条件に関わらず <see cref="CoverProbeResult.IsWall"/> で壁が残っているかが分かる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">sample の値が有限でないとき。</exception>
        public static CoverProbeResult Evaluate(CoverWallSample sample, Vector3 facing)
        {
            if (!sample.HasHit)
            {
                return CoverProbeResult.None;
            }

            if (!IsFinite(sample.Distance) || !IsFinite(sample.TopHeight) || !IsFinite(sample.Normal))
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample must be finite");
            }

            if (sample.Distance < 0f || sample.Distance > MaxEnterDistance + _boundaryEpsilon)
            {
                return CoverProbeResult.None;
            }

            var normalLength = sample.Normal.Length();
            if (normalLength < _minLength || Math.Abs(sample.Normal.Y / normalLength) > MaxWallNormalUp)
            {
                return CoverProbeResult.None;
            }

            var normal = Horizontal(sample.Normal);
            var height = sample.TopHeight < LowWallHeight ? CoverHeight.Low : CoverHeight.High;
            var edges = CoverEdge.None;
            if (!sample.WallOnLeft)
            {
                edges |= CoverEdge.Left;
            }

            if (!sample.WallOnRight)
            {
                edges |= CoverEdge.Right;
            }

            return new CoverProbeResult(true, FacesWall(facing, normal), height, edges, normal, sample.Distance);
        }

        /// <summary>
        /// 覗くときに壁沿いに横へ出る量 (m、右が正)。低い壁は立つだけなので 0、高い壁で端にいなければ 0。
        /// 両端とも開いている (狭い柱) なら preferredSide (負なら左、それ以外は右) へ出る。
        /// </summary>
        public static float PeekOffset(CoverProbeResult result, float preferredSide)
        {
            if (!result.IsWall || result.Height == CoverHeight.Low)
            {
                return 0f;
            }

            if (result.IsAtLeftEdge && result.IsAtRightEdge)
            {
                return preferredSide < 0f ? -PeekSideDistance : PeekSideDistance;
            }

            if (result.IsAtLeftEdge)
            {
                return -PeekSideDistance;
            }

            return result.IsAtRightEdge ? PeekSideDistance : 0f;
        }

        /// <summary>壁に張り付いたときの、体の中心から壁までの水平距離 (体の半径 + <see cref="WallGap"/>)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">bodyRadius が負か有限でないとき。</exception>
        public static float HoldDistance(float bodyRadius)
        {
            if (!(bodyRadius >= 0f) || float.IsInfinity(bodyRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(bodyRadius), bodyRadius, "bodyRadius must be finite and not negative");
            }

            return bodyRadius + WallGap;
        }

        /// <summary>壁に向いたときの右 (壁沿い、水平、長さ 1)。normal は壁のこちら向きの法線。</summary>
        /// <exception cref="ArgumentException">normal に水平の成分が無いとき。</exception>
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

        /// <summary>v の上下の成分を落として長さ 1 にする。水平の成分が無ければ 0。</summary>
        public static Vector3 Horizontal(Vector3 v)
        {
            var flat = new Vector3(v.X, 0f, v.Z);
            var length = flat.Length();
            return length < _minLength ? Vector3.Zero : flat / length;
        }

        private static bool FacesWall(Vector3 facing, Vector3 normal)
        {
            var forward = Horizontal(facing);
            if (forward == Vector3.Zero)
            {
                return false;
            }

            var cos = Vector3.Dot(forward, -normal);
            var limit = (float)Math.Cos(MaxEnterAngleDegrees * Math.PI / 180.0);
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
