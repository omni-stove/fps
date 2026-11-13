using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 乗り越え (<see cref="Vault.On"/>) の判定。物理で調べた前方の障害物 (<see cref="VaultObstacleSample"/>) の高さと奥行きから、
    /// 乗り越えられるか (<see cref="MotorSpec.VaultMaxHeight"/> 以下で、向こう側に立てる) と、その軌跡 (<see cref="VaultPath"/>) を決める。
    /// 軌跡は、障害物の手前へ寄りながら上端より <see cref="Clearance"/> 上まで上がり、向こう側の面から体の半径 + <see cref="WallGap"/> 先まで水平に越え、
    /// 足元の高さまで下りる。座標は Y が上、変位は始めたときの足元から。
    /// </summary>
    public static class VaultProbe
    {
        /// <summary>乗り越えられる、体の中心から手前の面までの水平距離の上限 (m)。</summary>
        public const float MaxReach = 1.0f;

        /// <summary>向いている方向と障害物へ向かう方向 (法線の逆) のなす角の上限 (度)。</summary>
        public const float MaxAngleDegrees = 45f;

        /// <summary>上端がこの高さ (足元から、m) 未満の障害物は段差として扱い、乗り越えない。</summary>
        public const float MinHeight = 0.3f;

        /// <summary>乗り越えられる奥行きの上限 (m)。これより厚い物は台で、向こう側へ降りられないとみなす。</summary>
        public const float MaxDepth = 1.0f;

        /// <summary>越えるときの、足元と障害物の上端の隙間 (m)。</summary>
        public const float Clearance = 0.15f;

        /// <summary>
        /// 寄ったとき・降りたときの、体の表面と障害物の面の隙間 (m)。
        /// CharacterController の既定の skinWidth (0.08) より広くし、寄る途中で面に押し返されて軌跡からずれないようにする。
        /// </summary>
        public const float WallGap = 0.1f;

        /// <summary>軌跡をたどる速さ (m/s)。体の半径 0.5 m で、0.8 m 先の高さ 1 m・厚さ 0.4 m の壁を越えるのに約 0.65 秒。</summary>
        public const float Speed = 6f;

        // 角度と距離の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        /// <summary>
        /// sample を判定する。facing は向いている方向 (上下の成分は無視する)、maxHeight は <see cref="MotorSpec.VaultMaxHeight"/>。
        /// 形が乗り越えられるものなら <see cref="PlanPath"/> で軌跡を作り、isPathClear (物理で軌跡の上を体が通れるか) が false なら
        /// <see cref="VaultVerdict.Blocked"/> にする。理由は <see cref="VaultVerdict"/> の並び順に調べ、最初に当たったものを返す。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">sample・facing・maxHeight・bodyRadius の値が不正なとき。</exception>
        /// <exception cref="ArgumentNullException">isPathClear が null のとき。</exception>
        public static VaultProbeResult Evaluate(
            VaultObstacleSample sample, Vector3 facing, float maxHeight, float bodyRadius, Func<VaultPath, bool> isPathClear)
        {
            if (!IsFinite(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must be finite");
            }

            if (!(maxHeight >= 0f) || float.IsInfinity(maxHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(maxHeight), maxHeight, "maxHeight must be finite and not negative");
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

            if (!IsFinite(sample.Distance) || !IsFinite(sample.TopHeight) || !IsFinite(sample.Normal) || float.IsNaN(sample.Depth))
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample must be finite (Depth may be positive infinity)");
            }

            if (sample.Distance < 0f || sample.Depth < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sample), "sample distance and depth must not be negative");
            }

            var height = sample.TopHeight;
            var depth = sample.Depth;
            if (sample.Distance > MaxReach + _boundaryEpsilon)
            {
                return new VaultProbeResult(VaultVerdict.TooFar, height, depth, Vector3.Zero, null);
            }

            var normalLength = sample.Normal.Length();
            var into = -CoverProbe.Horizontal(sample.Normal);
            if (normalLength <= 0f || Math.Abs(sample.Normal.Y / normalLength) > CoverProbe.MaxWallNormalUp || into == Vector3.Zero)
            {
                return new VaultProbeResult(VaultVerdict.NotAWall, height, depth, Vector3.Zero, null);
            }

            var verdict = Classify(into, facing, height, depth, maxHeight);
            if (verdict != VaultVerdict.Vault)
            {
                return new VaultProbeResult(verdict, height, depth, into, null);
            }

            var path = PlanPath(into, sample.Distance, height, depth, bodyRadius);
            verdict = isPathClear(path) ? VaultVerdict.Vault : VaultVerdict.Blocked;
            return new VaultProbeResult(verdict, height, depth, into, path);
        }

        /// <summary>
        /// direction (障害物へ向かう水平の向き) にある、手前の面まで distance・上端 topHeight・奥行き depth の障害物を、
        /// 半径 bodyRadius の体で越える軌跡を作る。頂点は始点・上がりきった所・越えきった所・降りきった所の 4 つ。
        /// </summary>
        /// <exception cref="ArgumentException">direction に水平の成分が無いとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">距離・高さ・奥行き・半径が負か有限でないとき。</exception>
        public static VaultPath PlanPath(Vector3 direction, float distance, float topHeight, float depth, float bodyRadius)
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
            return new VaultPath(points, Speed);
        }

        private static VaultVerdict Classify(Vector3 into, Vector3 facing, float height, float depth, float maxHeight)
        {
            if (!Faces(facing, into))
            {
                return VaultVerdict.NotFacing;
            }

            if (height < MinHeight)
            {
                return VaultVerdict.TooLow;
            }

            if (height > maxHeight + _boundaryEpsilon)
            {
                return VaultVerdict.TooHigh;
            }

            return depth > MaxDepth + _boundaryEpsilon ? VaultVerdict.TooDeep : VaultVerdict.Vault;
        }

        private static bool Faces(Vector3 facing, Vector3 into)
        {
            var forward = CoverProbe.Horizontal(facing);
            if (forward == Vector3.Zero)
            {
                return false;
            }

            var limit = (float)Math.Cos(MaxAngleDegrees * Math.PI / 180.0);
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
