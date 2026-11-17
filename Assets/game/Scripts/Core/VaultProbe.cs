using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// 乗り越え (<see cref="Vault.On"/>) の判定。物理で調べた前方の障害物 (<see cref="VaultObstacleSample"/>) の高さと奥行きから、
    /// 乗り越えられるか (<see cref="MotorSpec.VaultMaxHeight"/> 以下で、向こう側に立てる) と、その軌跡 (<see cref="VaultPath"/>) を決める。
    /// 届く距離・向きの角度・最低の高さ・奥行きの上限・速さは <see cref="MotorSpec"/> の Vault* の値を使う。
    /// 軌跡は、障害物の手前へ寄りながら上端より <see cref="Clearance"/> 上まで上がり、向こう側の面から体の半径 + <see cref="WallGap"/> 先まで水平に越え、
    /// 足元の高さまで下りる。座標は Y が上、変位は始めたときの足元から。
    /// <see cref="MotorSpec.VaultMaxDepth"/> より厚く、上に体が立てる (<see cref="MantleFootingDepth"/> 以上の) 台なら、越えずに上に乗る軌跡 (<see cref="PlanMantlePath"/>) にする。
    /// </summary>
    public static partial class VaultProbe
    {
        /// <summary>前方の障害物を探す Ray の、いちばん低いものの足元からの高さ (m)。既定の <see cref="MotorSpec.DefaultVaultMinHeight"/> より上で、低い障害物にも当たる高さ。</summary>
        public const float LowestProbeHeight = 0.35f;

        /// <summary>
        /// 前方を探す Ray を <see cref="LowestProbeHeight"/> から登れる高さの上限の手前まで並べる間隔 (m)。
        /// 台やアーチのように下が抜けて上が張り出した物は、低い Ray が下をくぐるので、張り出しの縁に当てるために上にも並べる。
        /// </summary>
        public const float ProbeHeightStep = 0.1f;

        /// <summary><see cref="ProbeHeightCount"/> の上限。登れる高さの上限がどれだけ高くても、1 回に飛ばす Ray をこれまでにする。</summary>
        public const int MaxProbeHeights = 32;

        /// <summary>手前の面の幅を測る Ray を、体の中心の正面から左右へ並べる間隔 (m)。</summary>
        public const float FaceWidthStep = 0.1f;

        /// <summary>手前の面の幅を測る Ray を、面からこれだけ手前から面へ向けて飛ばす (m)。</summary>
        public const float FaceWidthBack = 0.1f;

        /// <summary>
        /// 手前の面の幅を測る Ray が、<see cref="FaceWidthBack"/> よりこれだけ奥で当たっても同じ面とみなす (m)。
        /// 少し斜めの面や丸い面でも端まで続いているとみなすため。
        /// </summary>
        public const float FaceWidthSlack = 0.1f;

        /// <summary><see cref="FaceWidthProbeCount"/> の上限。</summary>
        public const int MaxFaceWidthProbes = 32;

        /// <summary>越えるときの、足元と障害物の上端の隙間 (m)。</summary>
        public const float Clearance = 0.15f;

        /// <summary>
        /// 寄ったとき・降りたときの、体の表面と障害物の面の隙間 (m)。
        /// CharacterController の既定の skinWidth (0.08) より広くし、寄る途中で面に押し返されて軌跡からずれないようにする。
        /// </summary>
        public const float WallGap = 0.1f;

        // 角度と距離の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        /// <summary>
        /// sample を判定する。facing は向いている方向 (上下の成分は無視する)、spec は高さの上限・届く距離・角度・最低の高さ・奥行き・速さを読む移動の設定。
        /// 形が乗り越えられるものなら <see cref="PlanPath"/> で軌跡を作り、isPathClear (物理で軌跡の上を体が通れるか) が false なら
        /// <see cref="VaultVerdict.Blocked"/> にする。理由は TooFar・NotAWall・NotFacing・TooLow・TooHigh・NarrowFace・TooDeep・Blocked の順に調べ、最初に当たったものを返す。
        /// 厚い台の上に乗れるときも <see cref="VaultVerdict.Vault"/> を返し、軌跡は台の上で終わる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">sample・facing・bodyRadius の値が不正なとき。</exception>
        /// <exception cref="ArgumentNullException">spec か isPathClear が null のとき。</exception>
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

        /// <summary>
        /// direction (障害物へ向かう水平の向き) にある、手前の面まで distance・上端 topHeight・奥行き depth の障害物を、
        /// 半径 bodyRadius の体で越える軌跡を作る。頂点は始点・上がりきった所・越えきった所・降りきった所の 4 つで、speed (<see cref="MotorSpec.VaultSpeed"/>) でたどる。
        /// </summary>
        /// <exception cref="ArgumentException">direction に水平の成分が無いとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">距離・高さ・奥行き・半径が負か有限でない、または speed が正の有限値でないとき。</exception>
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

        /// <summary>
        /// 登れる高さの上限 maxHeight までに前方を探す Ray の数。高さは <see cref="ProbeHeightAt"/> で、どれも maxHeight より低い。
        /// maxHeight が <see cref="LowestProbeHeight"/> 以下でも 1 本は飛ばす。多くても <see cref="MaxProbeHeights"/>。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">maxHeight が負か有限でないとき。</exception>
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

        /// <summary>index 番目 (0 から) の、前方を探す Ray の足元からの高さ (m)。0 番が <see cref="LowestProbeHeight"/> で、上へ <see cref="ProbeHeightStep"/> ずつ。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 から <see cref="MaxProbeHeights"/> - 1 の外のとき。</exception>
        public static float ProbeHeightAt(int index)
        {
            if (index < 0 || index >= MaxProbeHeights)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be from 0 to MaxProbeHeights - 1");
            }

            return LowestProbeHeight + (index * ProbeHeightStep);
        }

        /// <summary>
        /// normal の面を、乗り越えと崖のぼりが手前の面として扱えるか (<see cref="VaultVerdict.NotAWall"/> にならないか)。
        /// 前方を何本もの Ray で探すとき、床や張り出しの下面より壁に当たったものを選ぶのに使う。
        /// </summary>
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

        /// <summary>
        /// 高さを変えて飛ばした Ray の当たりから、手前の面にするものを選ぶ。壁 (<see cref="IsWall"/>) に当たったものがあればその中で、
        /// 無ければすべての中で、いちばん近いものを選ぶ。candidate が今の best より良ければ true。
        /// </summary>
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

        /// <summary>乗り越えとよじ登りに要る手前の面の幅 (m)。体の幅 (半径の 2 倍)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">bodyRadius が負か有限でないとき。</exception>
        public static float MinFaceWidth(float bodyRadius)
        {
            ValidateRadius(bodyRadius);
            return bodyRadius * 2f;
        }

        /// <summary>
        /// 手前の面の幅を、片側に何本の Ray で測るか。<see cref="FaceWidthOffset"/> のずれで並べ、いちばん外が体の半径になる。多くても <see cref="MaxFaceWidthProbes"/>。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">bodyRadius が負か有限でないとき。</exception>
        public static int FaceWidthProbeCount(float bodyRadius)
        {
            ValidateRadius(bodyRadius);
            return Math.Min(MaxFaceWidthProbes, (int)Math.Ceiling((bodyRadius / FaceWidthStep) - _boundaryEpsilon));
        }

        /// <summary>片側の index 番目 (0 から) の Ray の、体の中心の正面からの横のずれ (m)。<see cref="FaceWidthStep"/> ずつで、体の半径を超えない。</summary>
        /// <exception cref="ArgumentOutOfRangeException">bodyRadius が不正か、index が 0 から <see cref="FaceWidthProbeCount"/> - 1 の外のとき。</exception>
        public static float FaceWidthOffset(float bodyRadius, int index)
        {
            var count = FaceWidthProbeCount(bodyRadius);
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be from 0 to FaceWidthProbeCount - 1");
            }

            return Math.Min((index + 1) * FaceWidthStep, bodyRadius);
        }

        /// <summary>
        /// 手前の面 (point の所で、into の向きに入る面) の、左右に続く幅を測る。左右それぞれ <see cref="FaceWidthOffset"/> の所に
        /// 面の <see cref="FaceWidthBack"/> 手前から into の向きへ Ray を飛ばし (raycast)、壁 (<see cref="IsWall"/>) に
        /// <see cref="FaceWidthBack"/> + <see cref="FaceWidthSlack"/> 以内で当たる間だけ面が続いているとみなす。
        /// 両側の続いた分を足して返す。体の幅までしか測らないので、それより広い面は体の幅になる。
        /// </summary>
        /// <param name="point">手前の面に当たった点。</param>
        /// <param name="into">面へ向かう水平の向き。</param>
        /// <param name="bodyRadius">体の半径。</param>
        /// <param name="raycast">始点・向き・長さで Ray を飛ばし、当たれば法線と距離を返す関数。</param>
        /// <exception cref="ArgumentException">into に水平の成分が無いとき。</exception>
        /// <exception cref="ArgumentNullException">raycast が null のとき。</exception>
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

        /// <summary><see cref="MeasureFaceWidth"/> が面を探すのに飛ばす Ray。当たれば法線 (こちら向き) と始点からの距離を返す。</summary>
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
