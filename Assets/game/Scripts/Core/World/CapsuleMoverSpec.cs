using System;

namespace Shooter.Core
{
    public sealed class CapsuleMoverSpec
    {
        // 坂・段・skin の既定は CharacterController の既定と同じにする。
        public const float DefaultSlopeLimitDegrees = 45f;

        public const float DefaultStepOffset = 0.3f;

        public const float DefaultSkinWidth = 0.08f;

        public const int DefaultLayerMask = ~0;

        public static readonly CapsuleMoverSpec Default = new CapsuleMoverSpec(
            DefaultSlopeLimitDegrees, DefaultStepOffset, DefaultSkinWidth, DefaultLayerMask);

        public CapsuleMoverSpec(float slopeLimitDegrees, float stepOffset, float skinWidth, int layerMask)
        {
            if (!(slopeLimitDegrees >= 0f && slopeLimitDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slopeLimitDegrees), slopeLimitDegrees, "slopeLimitDegrees must be between 0 and 90");
            }

            if (!(stepOffset >= 0f) || float.IsInfinity(stepOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(stepOffset), stepOffset, "stepOffset must be finite and not negative");
            }

            if (!(skinWidth > 0f) || float.IsInfinity(skinWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(skinWidth), skinWidth, "skinWidth must be finite and positive");
            }

            SlopeLimitDegrees = slopeLimitDegrees;
            StepOffset = stepOffset;
            SkinWidth = skinWidth;
            LayerMask = layerMask;
            MinWalkableNormalY = (float)Math.Cos(slopeLimitDegrees * (Math.PI / 180.0));
        }

        public float SlopeLimitDegrees { get; }

        public float StepOffset { get; }

        public float SkinWidth { get; }

        public int LayerMask { get; }

        public float MinWalkableNormalY { get; }
    }
}
