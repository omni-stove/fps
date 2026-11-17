using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// はしご 1 本の形。<see cref="Bottom"/> は、はしごの面 (つかまる人が向き合う面) が地面と交わる線の真ん中で、
    /// そこから <see cref="Up"/> へ <see cref="Height"/> 上がった所が上の足場の高さ。つかまる人は面の手前 (<see cref="Facing"/> の逆側) にいて
    /// <see cref="Facing"/> を向き、上の足場は面の奥 (<see cref="Facing"/> 側) に広がる。座標はワールド (Y が上)。
    /// </summary>
    public readonly struct LadderSpan
    {
        /// <summary>上り下りしている間の、体の表面からはしごの面までの隙間 (m)。</summary>
        public const float FaceGap = 0.05f;

        /// <summary>上の端からつかまるとき、足元が上の足場の高さからこれだけずれていても足場の上にいるとみなす (m)。下の端の下も同じだけ許す。</summary>
        public const float HeightTolerance = 0.3f;

        /// <summary>上向きの単位ベクトル。</summary>
        public static readonly Vector3 Up = Vector3.UnitY;

        /// <param name="bottom">はしごの面の下端の真ん中 (ワールド)。各成分が有限値。</param>
        /// <param name="facing">つかまる人が向く向き。水平成分だけを使い、長さは問わない。水平成分が 0 でない有限値。</param>
        /// <param name="height">上の足場までの高さ (m)。0 より大きい有限値。</param>
        /// <param name="width">つかまれる幅 (m)。0 より大きい有限値。</param>
        public LadderSpan(Vector3 bottom, Vector3 facing, float height, float width)
        {
            if (!IsFinite(bottom))
            {
                throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "bottom must be finite");
            }

            var flat = new Vector3(facing.X, 0f, facing.Z);
            var length = flat.Length();
            if (!IsFinite(facing) || !(length > 1e-4f))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "facing must have a finite, non-zero horizontal part");
            }

            if (!(height > 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive and finite");
            }

            if (!(width > 0f) || float.IsInfinity(width))
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "width must be positive and finite");
            }

            Bottom = bottom;
            Facing = flat / length;
            Height = height;
            Width = width;
        }

        /// <summary>はしごの面の下端の真ん中 (ワールド)。</summary>
        public Vector3 Bottom { get; }

        /// <summary>つかまる人が向く水平の単位ベクトル。上の足場はこちら側にある。</summary>
        public Vector3 Facing { get; }

        /// <summary>上の足場までの高さ (m)。</summary>
        public float Height { get; }

        /// <summary>つかまれる幅 (m)。</summary>
        public float Width { get; }

        /// <summary>つかまる人から見た右 (水平の単位ベクトル)。</summary>
        public Vector3 Right => Vector3.Cross(Up, Facing);

        /// <summary>はしごの面の上端の真ん中 (上の足場の縁)。</summary>
        public Vector3 Top => Bottom + (Up * Height);

        /// <summary>
        /// 足元の高さ height (下端から、m) でつかまっている人の足元。半径 radius の体の表面が面から <see cref="FaceGap"/> 離れる所。
        /// </summary>
        public Vector3 FeetAt(float height, float radius)
        {
            return Bottom - (Facing * (radius + FaceGap)) + (Up * height);
        }

        /// <summary>上の端で降りた人の足元。上の足場の縁から、半径 radius の体がちょうど乗り切る所。</summary>
        public Vector3 TopExit(float radius)
        {
            return Top + (Facing * (radius + FaceGap));
        }

        /// <summary>NavMesh のリンクの下の端。面の手前に clearance (m) 離れた地面。</summary>
        public Vector3 LinkBottom(float clearance)
        {
            return Bottom - (Facing * clearance);
        }

        /// <summary>NavMesh のリンクの上の端。上の足場の縁から奥へ clearance (m) 入った所。</summary>
        public Vector3 LinkTop(float clearance)
        {
            return Top + (Facing * clearance);
        }

        /// <summary>
        /// リンクを渡る半径 radius の人が通る足元の点。上るなら はしごの下 → はしごの上 → 上の足場、下りるならその逆。
        /// リンクの端からはしごの下 (上の足場) までと、上の足場からリンクの端までは歩き、その間は上り下りする。
        /// </summary>
        public Vector3[] ClimbPath(bool ascending, float radius)
        {
            var foot = FeetAt(0f, radius);
            var head = FeetAt(Height, radius);
            var exit = TopExit(radius);
            return ascending ? new[] { foot, head, exit } : new[] { exit, head, foot };
        }

        /// <summary>ジャンプで離れたときの速度。はしごから遠ざかる水平の向きに speed。</summary>
        public Vector3 JumpOffVelocity(float speed)
        {
            return -Facing * speed;
        }

        /// <summary>
        /// 足元 feet・水平の速度 velocity・半径 radius の人が、今つかまるか。どれかの値が有限でなければつかまらない。
        /// <list type="bullet">
        /// <item><see cref="LadderEntry.Front"/>: 面の手前で体の表面が面から grabDistance 以内、足元が下端の少し下から
        /// 上の端の <see cref="MotorSpec.LadderTopEntryDepth"/> 手前までの高さにあり、<see cref="Facing"/> へ進んでいる。</item>
        /// <item><see cref="LadderEntry.Top"/>: 上の足場の上で体の中心が縁から radius + grabDistance 以内、足元が上の足場の高さにあり、
        /// 縁 (<see cref="Facing"/> の逆) へ進んでいる。</item>
        /// </list>
        /// どちらも横へは <see cref="Width"/> の半分まで。進んでいるとみなすのは、水平の速さが <see cref="MotorSpec.LadderMinApproachSpeed"/> 以上で、
        /// 向きの余弦が <see cref="MotorSpec.LadderMinApproachDot"/> 以上のとき。
        /// </summary>
        /// <param name="feet">人の足元の位置。</param>
        /// <param name="velocity">人の水平の速度 (m/s)。</param>
        /// <param name="radius">人の体の半径 (m)。</param>
        /// <param name="grabDistance">体の表面からはしごの面までの、つかまれる距離 (m)。0 以上。</param>
        /// <param name="motor">上から入る深さと近づく向き・速さを読む移動の設定。null 不可。</param>
        /// <exception cref="ArgumentNullException">motor が null のとき。</exception>
        public LadderEntry Probe(Vector3 feet, Vector3 velocity, float radius, float grabDistance, MotorSpec motor)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (!IsFinite(feet) || !IsFinite(velocity) || !(radius >= 0f) || float.IsInfinity(radius)
                || !(grabDistance >= 0f) || float.IsInfinity(grabDistance))
            {
                return LadderEntry.None;
            }

            var relative = feet - Bottom;
            var along = Vector3.Dot(relative, Facing);
            var lateral = Vector3.Dot(relative, Right);
            if (Math.Abs(lateral) > Width / 2f)
            {
                return LadderEntry.None;
            }

            var horizontal = new Vector3(velocity.X, 0f, velocity.Z);
            var speed = horizontal.Length();
            if (speed < motor.LadderMinApproachSpeed)
            {
                return LadderEntry.None;
            }

            var approach = Vector3.Dot(horizontal / speed, Facing);
            var reach = radius + grabDistance;
            var height = relative.Y;
            if (along <= 0f && along >= -reach
                && height >= -HeightTolerance && height <= Height - motor.LadderTopEntryDepth
                && approach >= motor.LadderMinApproachDot)
            {
                return LadderEntry.Front;
            }

            if (along >= 0f && along <= reach
                && Math.Abs(height - Height) <= HeightTolerance
                && -approach >= motor.LadderMinApproachDot)
            {
                return LadderEntry.Top;
            }

            return LadderEntry.None;
        }

        /// <summary>足元 feet の、下端からの高さ (m)。</summary>
        public float HeightOf(Vector3 feet)
        {
            return feet.Y - Bottom.Y;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
