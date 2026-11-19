using System;

namespace Shooter.Core
{
    public sealed class SightSpec
    {
        public const float DefaultAimedInProgress = 0.9f;

        public static readonly SightSpec Default = new SightSpec();

        public SightSpec(
            string reticleId = null,
            bool scopeOverlay = false,
            bool eyeViewInThirdPerson = false,
            float aimedInProgress = DefaultAimedInProgress,
            VisionMode visionMode = VisionMode.None)
        {
            ReticleId = SpecGuard.ReferenceId(reticleId);
            ScopeOverlay = scopeOverlay;
            EyeViewInThirdPerson = eyeViewInThirdPerson;
            if (!(aimedInProgress > 0f && aimedInProgress <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aimedInProgress), aimedInProgress, nameof(aimedInProgress) + " must be greater than 0 and at most 1");
            }

            AimedInProgress = aimedInProgress;
            VisionMode = SpecGuard.Defined(visionMode, nameof(visionMode));
        }

        public string ReticleId { get; }

        public bool HasReticle => ReticleId.Length > 0;

        public bool ScopeOverlay { get; }

        public bool EyeViewInThirdPerson { get; }

        public float AimedInProgress { get; }

        public VisionMode VisionMode { get; }

        public bool IsDefault =>
            !HasReticle && !ScopeOverlay && !EyeViewInThirdPerson && AimedInProgress == DefaultAimedInProgress
            && VisionMode == VisionMode.None;
    }
}
