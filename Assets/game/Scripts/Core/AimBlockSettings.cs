using System;

namespace Shooter.Core
{
    public sealed class AimBlockSettings
    {
        public const bool DefaultEnabled = true;

        public const float DefaultMargin = 0.3f;

        public static readonly AimBlockSettings Default = new AimBlockSettings(DefaultEnabled, DefaultMargin);

        public AimBlockSettings(bool enabled, float margin)
        {
            if (!(margin >= 0f) || float.IsInfinity(margin))
            {
                throw new ArgumentOutOfRangeException(nameof(margin), margin, "must be a finite number >= 0");
            }

            Enabled = enabled;
            Margin = margin;
        }

        public bool Enabled { get; }

        public float Margin { get; }
    }
}
