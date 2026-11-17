using System;

namespace Shooter.Core
{
    public sealed class StealthNoiseSpec
    {
        // 歩き (足音の大きさ 0.6) で 7 m、しゃがみ (0.2) で 2.4 m。しゃがめば見張りの真後ろを抜けられる。
        public const float DefaultFootstepNoiseRadius = 12f;

        public StealthNoiseSpec(
            float footstepNoiseRadius = DefaultFootstepNoiseRadius,
            float walkStride = FootstepCadence.DefaultWalkStride,
            float sprintStride = FootstepCadence.DefaultSprintStride,
            float crouchStride = FootstepCadence.DefaultCrouchStride,
            float walkVolume = FootstepCadence.DefaultWalkVolume,
            float sprintVolume = FootstepCadence.DefaultSprintVolume,
            float crouchVolume = FootstepCadence.DefaultCrouchVolume,
            float minLandingFallSpeed = FootstepCadence.DefaultMinLandingFallSpeed,
            float hardLandingFallSpeed = FootstepCadence.DefaultHardLandingFallSpeed,
            float minLandingVolume = FootstepCadence.DefaultMinLandingVolume)
        {
            if (!(footstepNoiseRadius >= 0f) || float.IsInfinity(footstepNoiseRadius))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(footstepNoiseRadius), footstepNoiseRadius, nameof(footstepNoiseRadius) + " must not be negative and must be finite");
            }

            // 歩幅・音量・着地の値の範囲は FootstepCadence が決めるので、ここで同じ検査を書き写さずに一度作って断らせる。
            _ = new FootstepCadence(
                walkStride, sprintStride, crouchStride, walkVolume, sprintVolume, crouchVolume, minLandingFallSpeed, hardLandingFallSpeed, minLandingVolume);

            FootstepNoiseRadius = footstepNoiseRadius;
            WalkStride = walkStride;
            SprintStride = sprintStride;
            CrouchStride = crouchStride;
            WalkVolume = walkVolume;
            SprintVolume = sprintVolume;
            CrouchVolume = crouchVolume;
            MinLandingFallSpeed = minLandingFallSpeed;
            HardLandingFallSpeed = hardLandingFallSpeed;
            MinLandingVolume = minLandingVolume;
        }

        public static StealthNoiseSpec Default { get; } = new StealthNoiseSpec();

        public float FootstepNoiseRadius { get; }
        public float WalkStride { get; }
        public float SprintStride { get; }
        public float CrouchStride { get; }
        public float WalkVolume { get; }
        public float SprintVolume { get; }
        public float CrouchVolume { get; }
        public float MinLandingFallSpeed { get; }
        public float HardLandingFallSpeed { get; }
        public float MinLandingVolume { get; }

        public FootstepCadence CreateCadence()
        {
            return new FootstepCadence(
                WalkStride, SprintStride, CrouchStride, WalkVolume, SprintVolume, CrouchVolume, MinLandingFallSpeed, HardLandingFallSpeed, MinLandingVolume);
        }
    }
}
