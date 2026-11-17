using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class FallenView
    {
        public const float DefaultSeconds = 0.8f;

        public const float DefaultEyeHeight = 0.25f;

        public const float DefaultRollDegrees = 75f;

        // 下を向くと倒れた自分の脚の方を見下ろしてしまうので、少し見上げる。
        public const float DefaultPitchDegrees = -10f;

        // 真横・真上より回すと上下が逆さに見えるため、傾きと上下の向きをここまでに収める。
        public const float MaxDegrees = 90f;

        public static readonly FallenView Default = new FallenView(DefaultSeconds, DefaultEyeHeight, DefaultRollDegrees, DefaultPitchDegrees);

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

        // 重さで速まるように見せるため、落ちる高さは始めをゆっくりにする。
        public float DropProgress(float elapsed)
        {
            var t = Linear(elapsed);
            return t * t;
        }

        // 崩れ落ちる勢いに見せるため、傾きと向きは始めを速くする。
        public float TurnProgress(float elapsed)
        {
            var rest = 1f - Linear(elapsed);
            return 1f - (rest * rest);
        }

        public Vector3 Eye(Vector3 deathEye, float groundY, float elapsed)
        {
            var restY = Math.Min(deathEye.Y, groundY + EyeHeight);
            var y = deathEye.Y + ((restY - deathEye.Y) * DropProgress(elapsed));
            return new Vector3(deathEye.X, y, deathEye.Z);
        }

        public float Pitch(float startPitch, float elapsed)
        {
            var start = IsFinite(startPitch) ? startPitch : 0f;
            return start + ((PitchDegrees - start) * TurnProgress(elapsed));
        }

        public float Roll(float elapsed)
        {
            return RollDegrees * TurnProgress(elapsed);
        }

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
