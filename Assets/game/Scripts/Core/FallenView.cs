using System;
using System.Numerics;

namespace Shooter.Core
{
    /// <summary>
    /// 倒れた主観 (<see cref="DeathCamera.Fallen"/>) の動き。倒れた目から床の近く (<see cref="EyeHeight"/>) まで <see cref="Seconds"/> 秒で落ち、
    /// その間に横へ <see cref="RollDegrees"/> 傾き、上下の向きを <see cref="PitchDegrees"/> へ寄せて、着いた後はそのまま止まる。
    /// 落ちる高さは重さで速まるように始めをゆっくり、傾きと向きは崩れ落ちる勢いで始めを速くする。
    /// 角度は <see cref="LookAngles"/> と同じく Unity の規則で、pitch は正が下向き、roll は正が左へ傾く (Unity の z 軸回り)。
    /// </summary>
    public sealed class FallenView
    {
        /// <summary>倒れ切るまでの秒数。</summary>
        public const float DefaultSeconds = 0.8f;

        /// <summary>倒れた後の目の、足元からの高さ (m)。</summary>
        public const float DefaultEyeHeight = 0.25f;

        /// <summary>倒れた後の横の傾き (度)。</summary>
        public const float DefaultRollDegrees = 75f;

        /// <summary>倒れた後の上下の向き (度、正が下)。下を向くと倒れた自分の脚の方を見下ろしてしまうので、少し見上げる。</summary>
        public const float DefaultPitchDegrees = -10f;

        /// <summary>傾きと上下の向きの上限 (度)。真横・真上より回すと上下が逆さに見えるため。</summary>
        public const float MaxDegrees = 90f;

        public static readonly FallenView Default = new FallenView(DefaultSeconds, DefaultEyeHeight, DefaultRollDegrees, DefaultPitchDegrees);

        /// <param name="seconds">倒れ切るまでの秒数。0 以上の有限値 (0 ならすぐ倒れた姿勢にする)。</param>
        /// <param name="eyeHeight">倒れた後の目の、足元からの高さ (m)。0 以上の有限値。</param>
        /// <param name="rollDegrees">倒れた後の横の傾き (度)。±<see cref="MaxDegrees"/> 以内。</param>
        /// <param name="pitchDegrees">倒れた後の上下の向き (度、正が下)。±<see cref="MaxDegrees"/> 以内。</param>
        public FallenView(float seconds, float eyeHeight, float rollDegrees, float pitchDegrees)
        {
            Seconds = RequireFiniteNonNegative(seconds, nameof(seconds));
            EyeHeight = RequireFiniteNonNegative(eyeHeight, nameof(eyeHeight));
            RollDegrees = RequireAngle(rollDegrees, nameof(rollDegrees));
            PitchDegrees = RequireAngle(pitchDegrees, nameof(pitchDegrees));
        }

        public float Seconds { get; }

        public float EyeHeight { get; }

        public float RollDegrees { get; }

        public float PitchDegrees { get; }

        /// <summary>倒れ始めてから elapsed 秒の、落ちる高さの進み (0 から 1)。始めはゆっくり、着く前に速い。</summary>
        public float DropProgress(float elapsed)
        {
            var t = Linear(elapsed);
            return t * t;
        }

        /// <summary>倒れ始めてから elapsed 秒の、傾きと向きの進み (0 から 1)。始めは速く、着く前にゆっくり。</summary>
        public float TurnProgress(float elapsed)
        {
            var rest = 1f - Linear(elapsed);
            return 1f - (rest * rest);
        }

        /// <summary>
        /// 倒れ始めてから elapsed 秒の目の位置。deathEye の真下へ、groundY (足元の高さ) から <see cref="EyeHeight"/> の高さまで落とす。
        /// しゃがみなどで目がすでにそれより低ければ落とさない。壁や床で縮めるのは呼び出し側。
        /// </summary>
        public Vector3 Eye(Vector3 deathEye, float groundY, float elapsed)
        {
            var restY = Math.Min(deathEye.Y, groundY + EyeHeight);
            var y = deathEye.Y + ((restY - deathEye.Y) * DropProgress(elapsed));
            return new Vector3(deathEye.X, y, deathEye.Z);
        }

        /// <summary>倒れ始めてから elapsed 秒の上下の向き (度、正が下)。startPitch (倒れた時の向き) から <see cref="PitchDegrees"/> へ寄せる。</summary>
        public float Pitch(float startPitch, float elapsed)
        {
            var start = IsFinite(startPitch) ? startPitch : 0f;
            return start + ((PitchDegrees - start) * TurnProgress(elapsed));
        }

        /// <summary>倒れ始めてから elapsed 秒の横の傾き (度)。</summary>
        public float Roll(float elapsed)
        {
            return RollDegrees * TurnProgress(elapsed);
        }

        /// <summary>elapsed 秒で倒れ切っているか。</summary>
        public bool IsSettled(float elapsed)
        {
            return Linear(elapsed) >= 1f;
        }

        private float Linear(float elapsed)
        {
            if (Seconds <= 0f || float.IsPositiveInfinity(elapsed))
            {
                return 1f;
            }

            if (!(elapsed > 0f))
            {
                return 0f;
            }

            return Math.Min(1f, elapsed / Seconds);
        }

        private static float RequireFiniteNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and not negative");
            }

            return value;
        }

        private static float RequireAngle(float value, string name)
        {
            if (!IsFinite(value) || Math.Abs(value) > MaxDegrees)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be within ±" + MaxDegrees + " degrees");
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
