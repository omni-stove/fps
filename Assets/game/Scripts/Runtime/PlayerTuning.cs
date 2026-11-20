using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class GameBootstrap
    {
        private readonly struct PlayerTuning
        {
            public PlayerTuning(
                int maxHealth,
                Vector3 thirdPersonOffset,
                float minBoomLength,
                float aimHoldMinDistance,
                float adsTransitionSeconds,
                MotorSettings motorSettings,
                TraversalSpec traversal)
            {
                MaxHealth = maxHealth;
                ThirdPersonOffset = thirdPersonOffset;
                MinBoomLength = minBoomLength;
                AimHoldMinDistance = aimHoldMinDistance;
                AdsTransitionSeconds = adsTransitionSeconds;
                MotorSettings = motorSettings;
                Traversal = traversal;
            }

            public int MaxHealth { get; }

            public Vector3 ThirdPersonOffset { get; }

            public float MinBoomLength { get; }

            public float AimHoldMinDistance { get; }

            public float AdsTransitionSeconds { get; }

            // null なら PlayerController が自分の引き先 (入口・Resources・既定) から読む。
            public MotorSettings MotorSettings { get; }

            public TraversalSpec Traversal { get; }
        }
    }
}
