namespace Shooter.Core
{
    public readonly struct AimAssistResult
    {
        public static AimAssistResult None { get; } = new AimAssistResult(1f, 0f, 0f);

        public AimAssistResult(float lookScale, float yawDelta, float pitchUpDelta)
        {
            LookScale = lookScale;
            YawDelta = yawDelta;
            PitchUpDelta = pitchUpDelta;
        }

        public float LookScale { get; }

        // LookAngles.Rotate にそのまま渡せるよう、yaw は正で右、pitch は正で上に揃える。
        public float YawDelta { get; }

        public float PitchUpDelta { get; }
    }
}
