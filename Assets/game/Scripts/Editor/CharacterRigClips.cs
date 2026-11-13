using System;
using System.Collections.Generic;

namespace Fps.Editor
{
    /// <summary>
    /// <see cref="CharacterRigBuilder"/> が作る AnimationClip の中身 (関節ごとの回転と腰の位置)。
    /// 角度は度で、関節のローカルの Euler 角。手足は下に垂れた向きが 0 で、X を負にすると前へ、正にすると後ろへ振る。
    /// 胴と頭は X を正にすると前へ傾く。左の手足は Z を負に、右は正にすると外へ開く。
    /// </summary>
    public static class CharacterRigClips
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string Crouch = "Crouch";
        public const string CrouchWalk = "CrouchWalk";
        public const string Aim = "Aim";
        public const string CrouchAim = "CrouchAim";
        public const string CoverLow = "CoverLow";
        public const string CoverHigh = "CoverHigh";
        public const string Downed = "Downed";
        public const string Dead = "Dead";

        /// <summary>立っているときの腰の高さ (m)。足の裏が足元 (0) に付く。</summary>
        public const float StandingHipsHeight = 0.95f;

        /// <summary>すべてのクリップの中身。名前は互いに異なる。</summary>
        public static IReadOnlyList<CharacterRigClipSpec> All()
        {
            return new[]
            {
                IdleClip(),
                WalkClip(),
                RunClip(),
                CrouchClip(),
                CrouchWalkClip(),
                AimClip(),
                CrouchAimClip(),
                CoverLowClip(),
                CoverHighClip(),
                DownedClip(),
                DeadClip(),
            };
        }

        private static CharacterRigClipSpec IdleClip()
        {
            var clip = new CharacterRigClipSpec(Idle, 3f, loop: true, RigWave3.Hold(0f, StandingHipsHeight, 0f));
            StandingArms(clip);
            clip.Rotate(CharacterRigBuilder.TorsoBone, new RigWave3(new RigWave(1f, 0.8f, 0f), RigWave.Zero, RigWave.Zero));
            return clip;
        }

        private static CharacterRigClipSpec WalkClip()
        {
            var clip = new CharacterRigClipSpec(Walk, 1f, loop: true, new RigWave3(RigWave.Zero, new RigWave(0.94f, 0.015f, 0.25f, 2), RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.HipsBone, new RigWave3(RigWave.Zero, new RigWave(0f, 4f, 0f), RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.TorsoBone, new RigWave3(RigWave.Of(4f), new RigWave(0f, -4f, 0f), RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.HeadBone, RigWave3.Hold(-4f, 0f, 0f));
            Stride(clip, thighBase: -3f, thighSwing: 24f, kneeBase: 18f, kneeSwing: 17f);
            ArmSwing(clip, upperBase: 0f, upperSwing: 18f, spread: 6f, elbow: -20f);
            return clip;
        }

        private static CharacterRigClipSpec RunClip()
        {
            var clip = new CharacterRigClipSpec(Run, 0.66f, loop: true, new RigWave3(RigWave.Zero, new RigWave(0.9f, 0.035f, 0.25f, 2), RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.HipsBone, new RigWave3(RigWave.Zero, new RigWave(0f, 6f, 0f), RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.TorsoBone, new RigWave3(RigWave.Of(12f), new RigWave(0f, -7f, 0f), RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.HeadBone, RigWave3.Hold(-12f, 0f, 0f));
            Stride(clip, thighBase: -12f, thighSwing: 42f, kneeBase: 50f, kneeSwing: 45f);
            ArmSwing(clip, upperBase: -5f, upperSwing: 38f, spread: 10f, elbow: -85f);
            return clip;
        }

        private static CharacterRigClipSpec CrouchClip()
        {
            // 太ももを -80° (ほぼ水平に前)、すねを +100° (太ももから折り返す) にすると、足の裏が腰から 0.56m 下に来る。
            var clip = new CharacterRigClipSpec(Crouch, 2f, loop: true, RigWave3.Hold(0f, 0.56f, 0f));
            CrouchBody(clip, torsoLean: 22f, headTilt: -18f);
            Stride(clip, thighBase: -80f, thighSwing: 0f, kneeBase: 100f, kneeSwing: 0f, spread: 8f);
            ArmSwing(clip, upperBase: -25f, upperSwing: 0f, spread: 8f, elbow: -60f);
            return clip;
        }

        private static CharacterRigClipSpec CrouchWalkClip()
        {
            var clip = new CharacterRigClipSpec(CrouchWalk, 1.1f, loop: true, new RigWave3(RigWave.Zero, new RigWave(0.55f, 0.01f, 0.25f, 2), RigWave.Zero));
            CrouchBody(clip, torsoLean: 24f, headTilt: -20f);
            Stride(clip, thighBase: -75f, thighSwing: 14f, kneeBase: 98f, kneeSwing: 10f, spread: 8f);
            ArmSwing(clip, upperBase: -25f, upperSwing: 10f, spread: 8f, elbow: -60f);
            return clip;
        }

        private static CharacterRigClipSpec AimClip()
        {
            var clip = new CharacterRigClipSpec(Aim, 2f, loop: true, RigWave3.Hold(0f, 0.94f, 0f));
            clip.Rotate(CharacterRigBuilder.TorsoBone, RigWave3.Hold(4f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.HeadBone, RigWave3.Hold(2f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.LeftThighBone, RigWave3.Hold(-8f, 0f, -4f));
            clip.Rotate(CharacterRigBuilder.RightThighBone, RigWave3.Hold(6f, 0f, 4f));
            clip.Rotate(CharacterRigBuilder.LeftShinBone, RigWave3.Hold(8f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.RightShinBone, RigWave3.Hold(8f, 0f, 0f));
            AimArms(clip);
            return clip;
        }

        private static CharacterRigClipSpec CrouchAimClip()
        {
            var clip = new CharacterRigClipSpec(CrouchAim, 2f, loop: true, RigWave3.Hold(0f, 0.56f, 0f));
            CrouchBody(clip, torsoLean: 12f, headTilt: -10f);
            Stride(clip, thighBase: -80f, thighSwing: 0f, kneeBase: 100f, kneeSwing: 0f, spread: 8f);
            AimArms(clip);
            return clip;
        }

        private static CharacterRigClipSpec CoverLowClip()
        {
            // しゃがみより深く折る。太もも -90°・すね +115° で足の裏が腰から 0.46m 下に来る。
            var clip = new CharacterRigClipSpec(CoverLow, 2f, loop: true, RigWave3.Hold(0f, 0.46f, 0f));
            CrouchBody(clip, torsoLean: 30f, headTilt: -15f);
            Stride(clip, thighBase: -90f, thighSwing: 0f, kneeBase: 115f, kneeSwing: 0f, spread: 10f);
            ArmSwing(clip, upperBase: -30f, upperSwing: 0f, spread: 5f, elbow: -100f);
            return clip;
        }

        private static CharacterRigClipSpec CoverHighClip()
        {
            var clip = new CharacterRigClipSpec(CoverHigh, 2f, loop: true, RigWave3.Hold(0f, StandingHipsHeight, 0f));
            clip.Rotate(CharacterRigBuilder.TorsoBone, RigWave3.Hold(-3f, 0f, 0f));
            Stride(clip, thighBase: 0f, thighSwing: 0f, kneeBase: 0f, kneeSwing: 0f, spread: 4f);
            clip.Rotate(CharacterRigBuilder.RightUpperArmBone, RigWave3.Hold(-20f, 0f, -10f));
            clip.Rotate(CharacterRigBuilder.RightForearmBone, RigWave3.Hold(-120f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.LeftUpperArmBone, RigWave3.Hold(-35f, 0f, 20f));
            clip.Rotate(CharacterRigBuilder.LeftForearmBone, RigWave3.Hold(-100f, 0f, 0f));
            return clip;
        }

        private static CharacterRigClipSpec DownedClip()
        {
            // 腰を地面近くに落として後ろへ 55° 倒し、左手を後ろについて上体を支える。太ももは -35° で地面と水平になる。
            var clip = new CharacterRigClipSpec(Downed, 2f, loop: true, RigWave3.Hold(0f, 0.16f, 0f));
            clip.Rotate(CharacterRigBuilder.HipsBone, RigWave3.Hold(-55f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.TorsoBone, new RigWave3(new RigWave(10f, 2f, 0f), RigWave.Zero, RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.HeadBone, RigWave3.Hold(25f, 0f, 0f));
            Stride(clip, thighBase: -35f, thighSwing: 0f, kneeBase: 10f, kneeSwing: 0f, spread: 8f);
            clip.Rotate(CharacterRigBuilder.LeftUpperArmBone, RigWave3.Hold(80f, 0f, -10f));
            clip.Rotate(CharacterRigBuilder.LeftForearmBone, RigWave3.Hold(-5f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.RightUpperArmBone, RigWave3.Hold(-40f, 0f, -20f));
            clip.Rotate(CharacterRigBuilder.RightForearmBone, RigWave3.Hold(-70f, 0f, 0f));
            return clip;
        }

        private static CharacterRigClipSpec DeadClip()
        {
            // 腰ごと -90° 倒して仰向けにする。胴の厚みの半分だけ腰を浮かせる。倒れ込む動きは遷移の補間に任せ、最後の姿勢で止める。
            var clip = new CharacterRigClipSpec(Dead, 1f, loop: false, RigWave3.Hold(0f, 0.12f, 0f));
            clip.Rotate(CharacterRigBuilder.HipsBone, RigWave3.Hold(-90f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.HeadBone, RigWave3.Hold(-10f, 0f, 0f));
            Stride(clip, thighBase: 0f, thighSwing: 0f, kneeBase: 0f, kneeSwing: 0f, spread: 6f);
            clip.Rotate(CharacterRigBuilder.LeftUpperArmBone, RigWave3.Hold(0f, 0f, -60f));
            clip.Rotate(CharacterRigBuilder.RightUpperArmBone, RigWave3.Hold(0f, 0f, 60f));
            clip.Rotate(CharacterRigBuilder.LeftForearmBone, RigWave3.Hold(-10f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.RightForearmBone, RigWave3.Hold(-10f, 0f, 0f));
            return clip;
        }

        private static void StandingArms(CharacterRigClipSpec clip)
        {
            ArmSwing(clip, upperBase: 0f, upperSwing: 0f, spread: 6f, elbow: -10f);
        }

        private static void CrouchBody(CharacterRigClipSpec clip, float torsoLean, float headTilt)
        {
            clip.Rotate(CharacterRigBuilder.TorsoBone, RigWave3.Hold(torsoLean, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.HeadBone, RigWave3.Hold(headTilt, 0f, 0f));
        }

        // 右足は左足から半周期ずらす。膝は太ももが前へ振り出される途中 (左足なら周期の半ば) で最も曲がる。
        private static void Stride(CharacterRigClipSpec clip, float thighBase, float thighSwing, float kneeBase, float kneeSwing, float spread = 0f)
        {
            clip.Rotate(CharacterRigBuilder.LeftThighBone, new RigWave3(new RigWave(thighBase, thighSwing, 0f), RigWave.Zero, RigWave.Of(-spread)));
            clip.Rotate(CharacterRigBuilder.RightThighBone, new RigWave3(new RigWave(thighBase, thighSwing, 0.5f), RigWave.Zero, RigWave.Of(spread)));
            clip.Rotate(CharacterRigBuilder.LeftShinBone, new RigWave3(new RigWave(kneeBase, kneeSwing, 0.75f), RigWave.Zero, RigWave.Zero));
            clip.Rotate(CharacterRigBuilder.RightShinBone, new RigWave3(new RigWave(kneeBase, kneeSwing, 0.25f), RigWave.Zero, RigWave.Zero));
        }

        // 腕は同じ側の足と逆に振る (左足が後ろのとき左腕は前)。
        private static void ArmSwing(CharacterRigClipSpec clip, float upperBase, float upperSwing, float spread, float elbow)
        {
            clip.Rotate(CharacterRigBuilder.LeftUpperArmBone, new RigWave3(new RigWave(upperBase, -upperSwing, 0f), RigWave.Zero, RigWave.Of(-spread)));
            clip.Rotate(CharacterRigBuilder.RightUpperArmBone, new RigWave3(new RigWave(upperBase, -upperSwing, 0.5f), RigWave.Zero, RigWave.Of(spread)));
            clip.Rotate(CharacterRigBuilder.LeftForearmBone, RigWave3.Hold(elbow, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.RightForearmBone, RigWave3.Hold(elbow, 0f, 0f));
        }

        // 右手で銃把を握って肘を下げ、左手を前へ伸ばして銃身を支える。
        private static void AimArms(CharacterRigClipSpec clip)
        {
            clip.Rotate(CharacterRigBuilder.RightUpperArmBone, RigWave3.Hold(-45f, 0f, -15f));
            clip.Rotate(CharacterRigBuilder.RightForearmBone, RigWave3.Hold(-75f, 0f, 0f));
            clip.Rotate(CharacterRigBuilder.LeftUpperArmBone, RigWave3.Hold(-70f, 0f, 25f));
            clip.Rotate(CharacterRigBuilder.LeftForearmBone, RigWave3.Hold(-30f, 0f, 0f));
        }
    }

    /// <summary>1 本のクリップの中身。指定しなかった関節は回転 0 (立って腕を垂らした向き) でキーにする。</summary>
    public sealed class CharacterRigClipSpec
    {
        private readonly Dictionary<string, RigWave3> _rotations = new Dictionary<string, RigWave3>(StringComparer.Ordinal);

        public string Name { get; }

        /// <summary>クリップの長さ (秒)。揺れのあるクリップでは 1 周期。</summary>
        public float Length { get; }

        public bool Loop { get; }

        /// <summary><see cref="CharacterRigBuilder.HipsBone"/> のローカル位置 (m)。</summary>
        public RigWave3 HipsPosition { get; }

        public CharacterRigClipSpec(string name, float length, bool loop, RigWave3 hipsPosition)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("clip name must not be empty", nameof(name));
            }

            if (!(length > 0f) || float.IsInfinity(length))
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "length must be positive and finite");
            }

            Name = name;
            Length = length;
            Loop = loop;
            HipsPosition = hipsPosition;
        }

        /// <summary>bone の回転 (度)。指定していなければ 0。</summary>
        public RigWave3 Rotation(string bone)
        {
            return _rotations.TryGetValue(bone, out var rotation) ? rotation : RigWave3.Hold(0f, 0f, 0f);
        }

        /// <summary>bone の回転 (度) を rotation にする。既に指定していれば上書きする。</summary>
        public void Rotate(string bone, RigWave3 rotation)
        {
            if (string.IsNullOrEmpty(bone))
            {
                throw new ArgumentException("bone must not be empty", nameof(bone));
            }

            _rotations[bone] = rotation;
        }

        /// <summary>回転を指定した関節の名前。</summary>
        public IEnumerable<string> RotatedBones => _rotations.Keys;
    }

    /// <summary>
    /// クリップの 1 つの値の動き。<c>Base + Amplitude * sin(2π (Frequency * t / 長さ + Phase))</c>。
    /// Amplitude が 0 なら Base のまま動かない。
    /// </summary>
    public readonly struct RigWave
    {
        public static readonly RigWave Zero = new RigWave(0f, 0f, 0f);

        public float Base { get; }
        public float Amplitude { get; }

        /// <summary>周期に対するずれ。0〜1。</summary>
        public float Phase { get; }

        /// <summary>クリップ 1 本の間に揺れる回数。1 以上。</summary>
        public int Frequency { get; }

        public bool IsConstant => Amplitude == 0f;

        public RigWave(float baseValue, float amplitude, float phase, int frequency = 1)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue) || float.IsNaN(amplitude) || float.IsInfinity(amplitude))
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), "baseValue and amplitude must be finite");
            }

            if (!(phase >= 0f && phase < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "phase must be at least 0 and less than 1");
            }

            if (frequency < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "frequency must be at least 1");
            }

            Base = baseValue;
            Amplitude = amplitude;
            Phase = phase;
            Frequency = frequency;
        }

        public static RigWave Of(float value)
        {
            return new RigWave(value, 0f, 0f);
        }
    }

    /// <summary>x・y・z の 3 つの <see cref="RigWave"/>。</summary>
    public readonly struct RigWave3
    {
        public RigWave X { get; }
        public RigWave Y { get; }
        public RigWave Z { get; }

        public RigWave3(RigWave x, RigWave y, RigWave z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static RigWave3 Hold(float x, float y, float z)
        {
            return new RigWave3(RigWave.Of(x), RigWave.Of(y), RigWave.Of(z));
        }
    }
}
