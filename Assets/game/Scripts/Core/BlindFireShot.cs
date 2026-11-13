using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 遮蔽物に隠れたまま撃つブラインドファイアの、弾の始点と向き。座標は Y が上、左右は壁に向いたときの左右 (<see cref="CoverProbe.RightAlong"/>)。
    /// 低い壁では壁の上から、高い壁では端の横から撃ち、向きは狙った向きを中心とする円錐の中で一様にばらす。
    /// </summary>
    public static class BlindFireShot
    {
        public const string LogPrefix = "[Combat] blind-fire";

        /// <summary>低い壁で、始点を壁の上端からどれだけ上に置くか (m)。</summary>
        public const float LowWallClearance = 0.15f;

        /// <summary>低い壁で、始点を壁の法線の向き (こちら向き) へどれだけ出すか (m)。</summary>
        public const float LowWallNormalOffset = 0.1f;

        /// <summary>高い壁で、始点を体の半径よりどれだけ外へ出すか (m)。</summary>
        public const float HighWallSideMargin = 0.15f;

        /// <summary>散らばりの半角の上限 (度)。<see cref="WeaponSpec.BlindFireSpreadDegrees"/> と同じ。</summary>
        public const float MaxSpreadDegrees = 90f;

        private const float _minLength = 1e-6f;

        /// <summary>
        /// 高い壁で横に出る側。左の端だけなら -1、右の端だけなら +1、両側とも端 (狭い柱) なら preferredSide (負なら -1、それ以外は +1)、
        /// どちらの端でもなければ 0 (撃てない)。覗くときの側 (<see cref="CoverProbe.PeekOffset"/>) と同じ決め方にする。
        /// </summary>
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

        /// <summary>
        /// 弾の始点。低い壁なら eye の水平位置を壁の上端 + <see cref="LowWallClearance"/> の高さに上げ、法線の向きへ <see cref="LowWallNormalOffset"/> 出した点。
        /// 高い壁なら eye から端の側 (<see cref="EdgeSide"/>) へ bodyRadius + <see cref="HighWallSideMargin"/> 横に出した点 (高さは目のまま)。
        /// 高い壁で端にいなければ撃てないので false を返し、origin は eye のまま。
        /// </summary>
        /// <param name="height">張り付いている壁の高さの区分。</param>
        /// <param name="edges">壁が途切れる側。</param>
        /// <param name="preferredSide">両側とも端のときに出る側 (負なら左)。</param>
        /// <param name="normal">壁のこちら向きの法線。上下の成分は無視する。</param>
        /// <param name="coverTopY">壁の上端の高さ (ワールドの y)。</param>
        /// <param name="bodyRadius">体の半径 (m)。0 以上。</param>
        /// <param name="eye">ふだんの射撃の始点 (目)。体の中心の真上にある。</param>
        /// <param name="origin">ブラインドファイアの始点。</param>
        /// <exception cref="ArgumentOutOfRangeException">値が有限でない・bodyRadius が負・height が定義に無いとき。</exception>
        /// <exception cref="ArgumentException">normal に水平の成分が無いとき。</exception>
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

        /// <summary>
        /// direction を中心とし、半角 halfAngleDegrees の円錐の中で一様 (立体角あたり同じ確からしさ) に選んだ向き (長さ 1)。
        /// 0° なら direction を長さ 1 にしたものをそのまま返し、乱数は引かない。
        /// </summary>
        /// <param name="direction">狙った向き。長さは問わない。</param>
        /// <param name="halfAngleDegrees">散らばりの半角 (度)。0 以上 <see cref="MaxSpreadDegrees"/> 以下。</param>
        /// <param name="random">乱数。テストで固定できるよう外から渡す。</param>
        /// <exception cref="ArgumentNullException">random が null のとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">direction が有限でないか長さ 0、halfAngleDegrees が範囲外のとき。</exception>
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

        /// <summary>
        /// direction を中心とし、半角 halfAngleDegrees の円錐の中で、種 (playerId・shotIndex) から決めた向き (長さ 1)。
        /// ネット対戦で持ち主の予測とサーバーの判定を揃えるために乱数ではなく種で決める。
        /// 武器のばらつき (<see cref="SpreadPattern.Direction(Vector3, float, int, int)"/>) とは別の系列を使うので、同じ種でも別の向きになる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">direction が有限でないか長さ 0、halfAngleDegrees が範囲外のとき。</exception>
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

        /// <summary>ブラインドファイアのログ。例: <c>[Combat] blind-fire spread=8</c>。</summary>
        public static string FormatLog(float spreadDegrees)
        {
            return LogPrefix + " spread=" + spreadDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // forward (長さ 1) に直交し、互いにも直交する長さ 1 の 2 本。forward と平行になりにくい軸を補助に選ぶ。
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
