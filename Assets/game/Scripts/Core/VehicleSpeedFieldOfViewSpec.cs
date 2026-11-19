using System;

namespace Shooter.Core
{
    public sealed class VehicleSpeedFieldOfViewSpec
    {
        public const float DefaultStartSpeed = 10f;
        public const float DefaultFullSpeed = 40f;
        public const float DefaultExtraDegrees = 0f;
        public const float DefaultBlendSeconds = 0.4f;
        public const float MaxExtraDegrees = 90f;

        public static readonly VehicleSpeedFieldOfViewSpec Default = new VehicleSpeedFieldOfViewSpec();

        public VehicleSpeedFieldOfViewSpec(
            float startSpeed = DefaultStartSpeed,
            float fullSpeed = DefaultFullSpeed,
            float extraDegrees = DefaultExtraDegrees,
            float blendSeconds = DefaultBlendSeconds)
        {
            if (!(startSpeed >= 0f) || float.IsInfinity(startSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(startSpeed), startSpeed, "startSpeed must not be negative and must be finite");
            }

            if (!(fullSpeed > startSpeed) || float.IsInfinity(fullSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(fullSpeed), fullSpeed, "fullSpeed must be finite and greater than startSpeed");
            }

            if (!(extraDegrees >= 0f && extraDegrees <= MaxExtraDegrees))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(extraDegrees), extraDegrees, $"extraDegrees must be between 0 and {MaxExtraDegrees}");
            }

            if (!(blendSeconds >= 0f) || float.IsInfinity(blendSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(blendSeconds), blendSeconds, "blendSeconds must not be negative and must be finite");
            }

            StartSpeed = startSpeed;
            FullSpeed = fullSpeed;
            ExtraDegrees = extraDegrees;
            BlendSeconds = blendSeconds;
        }

        public float StartSpeed { get; }

        public float FullSpeed { get; }

        public float ExtraDegrees { get; }

        public float BlendSeconds { get; }

        public bool IsEnabled => ExtraDegrees > 0f;

        // 速さの絶対値を使わないのは、後ろへ進むときに広げないため。
        public float ExtraDegreesAt(float forwardSpeed)
        {
            if (float.IsNaN(forwardSpeed) || forwardSpeed <= StartSpeed)
            {
                return 0f;
            }

            if (forwardSpeed >= FullSpeed)
            {
                return ExtraDegrees;
            }

            return ExtraDegrees * ((forwardSpeed - StartSpeed) / (FullSpeed - StartSpeed));
        }

        // 1 フレームで寄せる割合を秒数から指数で出すのは、描画の頻度が変わっても同じ秒数で寄るようにするため。
        public float Blend(float current, float goal, float deltaSeconds)
        {
            if (float.IsNaN(current) || float.IsInfinity(current))
            {
                return goal;
            }

            var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Math.Max(0f, deltaSeconds);
            if (BlendSeconds <= 0f)
            {
                return goal;
            }

            var t = 1f - (float)Math.Exp(-seconds / BlendSeconds);
            return current + ((goal - current) * t);
        }
    }
}
