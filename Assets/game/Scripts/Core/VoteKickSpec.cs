using System;

namespace Shooter.Core
{
    // 投票キックは専用サーバーだけが動かすので、サーバーごとの NetMatchSettings に持たせる。既定を無効にするのは、
    // 公開サーバーの持ち主が選ばないうちに、他の人を外せる口を開けないため。
    public sealed class VoteKickSpec
    {
        public const bool DefaultEnabled = false;
        public const float DefaultRequiredYesFraction = 0.6f;
        public const float DefaultVoteSeconds = 30f;
        public const float DefaultCooldownSeconds = 120f;

        public static VoteKickSpec Default { get; } = new VoteKickSpec();

        public bool Enabled { get; }

        public float RequiredYesFraction { get; }

        public float VoteSeconds { get; }

        public float CooldownSeconds { get; }

        public VoteKickSpec(
            bool enabled = DefaultEnabled,
            float requiredYesFraction = DefaultRequiredYesFraction,
            float voteSeconds = DefaultVoteSeconds,
            float cooldownSeconds = DefaultCooldownSeconds)
        {
            if (!(requiredYesFraction > 0f) || !(requiredYesFraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredYesFraction), requiredYesFraction, "requiredYesFraction must be more than 0 and at most 1");
            }

            if (!(voteSeconds > 0f) || float.IsInfinity(voteSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(voteSeconds), voteSeconds, "voteSeconds must be positive and finite");
            }

            if (!(cooldownSeconds >= 0f) || float.IsInfinity(cooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), cooldownSeconds, "cooldownSeconds must not be negative and must be finite");
            }

            Enabled = enabled;
            RequiredYesFraction = requiredYesFraction;
            VoteSeconds = voteSeconds;
            CooldownSeconds = cooldownSeconds;
        }
    }
}
