using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class ExtractionSpec
    {
        public const float DefaultRaidSeconds = 900f;
        public const float DefaultExtractHoldSeconds = 8f;
        public const int DefaultOpenPoints = 2;
        public const float DefaultOpenAfterSeconds = 0f;
        public const bool DefaultKeepCarriedOnDeath = false;

        public static ExtractionSpec Default { get; } = new ExtractionSpec();

        public ExtractionSpec(
            float raidSeconds = DefaultRaidSeconds,
            float extractHoldSeconds = DefaultExtractHoldSeconds,
            int openPoints = DefaultOpenPoints,
            float openAfterSeconds = DefaultOpenAfterSeconds,
            bool keepCarriedOnDeath = DefaultKeepCarriedOnDeath)
        {
            RaidSeconds = SpecGuard.PositiveFinite(raidSeconds, nameof(raidSeconds));
            ExtractHoldSeconds = SpecGuard.NonNegativeFinite(extractHoldSeconds, nameof(extractHoldSeconds));
            OpenPoints = SpecGuard.AtLeast(openPoints, 1, nameof(openPoints));
            OpenAfterSeconds = SpecGuard.NonNegativeFinite(openAfterSeconds, nameof(openAfterSeconds));

            // 脱出地点が開く前か、開いてから立ち止まり切る前に時間が切れると、誰も脱出できない。
            if (OpenAfterSeconds + ExtractHoldSeconds >= RaidSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(openAfterSeconds),
                    openAfterSeconds,
                    nameof(openAfterSeconds) + " plus extractHoldSeconds must be shorter than raidSeconds ("
                    + raidSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }

            KeepCarriedOnDeath = keepCarriedOnDeath;
        }

        public float RaidSeconds { get; }

        public float ExtractHoldSeconds { get; }

        public int OpenPoints { get; }

        public float OpenAfterSeconds { get; }

        public bool KeepCarriedOnDeath { get; }
    }
}
