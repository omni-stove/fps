using System;

namespace Shooter.Core
{
    // 射程・捕捉の秒・円錐を持たないのは、捕捉に使う武器 (HomingSpec と RoundSpec.Range) の値をそのまま使い、置き場を 1 つにするため。
    public sealed class LockOnTurretSpec
    {
        public const float DefaultTurnDegreesPerSecond = 120f;

        // 歩いている人や車を捉えず、空にいる物だけを狙う高さ。人が跳んでも届かない高さにしてある。
        public const float DefaultMinTargetHeight = 5f;

        public LockOnTurretSpec(
            float turnDegreesPerSecond = DefaultTurnDegreesPerSecond,
            float minTargetHeight = DefaultMinTargetHeight,
            string targetTag = null)
        {
            TurnDegreesPerSecond = SpecGuard.PositiveFinite(turnDegreesPerSecond, nameof(turnDegreesPerSecond));
            if (float.IsNaN(minTargetHeight) || float.IsInfinity(minTargetHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(minTargetHeight), minTargetHeight, "minTargetHeight must be finite");
            }

            MinTargetHeight = minTargetHeight;
            if (targetTag != null && targetTag.Length > 0 && string.IsNullOrWhiteSpace(targetTag))
            {
                throw new ArgumentException("targetTag must not be blank; leave it empty to take anyone", nameof(targetTag));
            }

            TargetTag = targetTag ?? string.Empty;
        }

        public static LockOnTurretSpec Default => new LockOnTurretSpec();

        public float TurnDegreesPerSecond { get; }

        // タレットの原点からの高さ (m)。負にすると、タレットより低い所にいる相手も狙う。
        public float MinTargetHeight { get; }

        // 狙う相手の体 (またはその親) のタグ。空なら誰でも狙う。標的の人形を外し、プレイヤーだけを狙わせるのに使う。
        public string TargetTag { get; }

        public bool IsHighEnough(float turretHeight, float targetHeight)
        {
            return targetHeight - turretHeight >= MinTargetHeight;
        }

        public bool AcceptsTag(string tag)
        {
            return TargetTag.Length == 0 || string.Equals(TargetTag, tag, StringComparison.Ordinal);
        }
    }
}
