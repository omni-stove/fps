using System;

namespace Shooter.Core
{
    /// <summary>
    /// 目から狙う点までの線が手前の物に遮られているとき、照準で知らせるための値 (<see cref="AimSolver.IsBlocked"/>)。
    /// </summary>
    public sealed class AimBlockSettings
    {
        public const bool DefaultEnabled = true;

        /// <summary>狙う点からこれより手前の命中だけを遮りとみなす既定の距離 (m)。</summary>
        public const float DefaultMargin = 0.3f;

        public static readonly AimBlockSettings Default = new AimBlockSettings(DefaultEnabled, DefaultMargin);

        /// <exception cref="ArgumentOutOfRangeException">margin が負か有限でないとき。</exception>
        public AimBlockSettings(bool enabled, float margin)
        {
            if (!(margin >= 0f) || float.IsInfinity(margin))
            {
                throw new ArgumentOutOfRangeException(nameof(margin), margin, "must be a finite number >= 0");
            }

            Enabled = enabled;
            Margin = margin;
        }

        /// <summary>遮られているときに照準の見た目を変え、遮られた点に印を出すか。</summary>
        public bool Enabled { get; }

        /// <summary>狙う点からこれより手前の命中だけを遮りとみなす距離 (m)。</summary>
        public float Margin { get; }
    }
}
