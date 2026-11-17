using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class StreakSpec
    {
        public const string UavRewardId = "uav";
        public const string AirstrikeRewardId = "airstrike";

        public const string DefaultUavNameKey = "streak.uav";
        public const string DefaultAirstrikeNameKey = "streak.airstrike";

        public const int DefaultUavKillsRequired = 3;
        public const int DefaultAirstrikeKillsRequired = 5;

        public StreakSpec(IReadOnlyList<StreakReward> rewards = null, StreakUavSpec uav = null, StreakAirstrikeSpec airstrike = null)
        {
            Rewards = rewards == null ? DefaultRewards : CheckRewards(rewards);
            Uav = uav ?? StreakUavSpec.Default;
            Airstrike = airstrike ?? StreakAirstrikeSpec.Default;
        }

        public static IReadOnlyList<StreakReward> DefaultRewards { get; } = new[]
        {
            new StreakReward(UavRewardId, DefaultUavNameKey, DefaultUavKillsRequired),
            new StreakReward(AirstrikeRewardId, DefaultAirstrikeNameKey, DefaultAirstrikeKillsRequired),
        };

        public static IReadOnlyList<string> KnownRewardIds { get; } = new[] { UavRewardId, AirstrikeRewardId };

        public static StreakSpec Default { get; } = new StreakSpec();

        public IReadOnlyList<StreakReward> Rewards { get; }

        public StreakUavSpec Uav { get; }

        public StreakAirstrikeSpec Airstrike { get; }

        public StreakReward FindReward(string id)
        {
            foreach (var reward in Rewards)
            {
                if (string.Equals(reward.Id, id, StringComparison.Ordinal))
                {
                    return reward;
                }
            }

            return null;
        }

        private static StreakReward[] CheckRewards(IReadOnlyList<StreakReward> rewards)
        {
            var copy = new StreakReward[rewards.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                var reward = rewards[i] ?? throw new ArgumentException("rewards must not contain null", nameof(rewards));

                // 動かす側の無い Id は、使っても何も起きないまま枠だけが埋まる。綴りの誤りを読み込みで止める。
                if (!IsKnown(reward.Id))
                {
                    throw new ArgumentException(
                        "rewards contains an unknown reward id '" + reward.Id + "'; known ids are " + string.Join(", ", KnownRewardIds), nameof(rewards));
                }

                // StreakTracker は報酬を Id で数えるので、同じ Id が 2 つあると片方が得られない。
                if (!seen.Add(reward.Id))
                {
                    throw new ArgumentException("rewards contains the reward id '" + reward.Id + "' twice", nameof(rewards));
                }

                copy[i] = reward;
            }

            return copy;
        }

        private static bool IsKnown(string id)
        {
            foreach (var known in KnownRewardIds)
            {
                if (string.Equals(known, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class StreakReward
    {
        public StreakReward(string id, string nameKey, int killsRequired)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            Id = id.Trim();
            DisplayNameKey = SpecGuard.NameKey(nameKey);
            KillsRequired = SpecGuard.AtLeast(killsRequired, 1, nameof(killsRequired));
        }

        public string Id { get; }

        public string DisplayNameKey { get; }

        public int KillsRequired { get; }
    }

    public sealed class StreakUavSpec
    {
        public const float DefaultDurationSeconds = 20f;

        public const float DefaultRange = 80f;

        public static readonly StreakUavSpec Default = new StreakUavSpec();

        public StreakUavSpec(float durationSeconds = DefaultDurationSeconds, float range = DefaultRange)
        {
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            Range = SpecGuard.PositiveFinite(range, nameof(range));
        }

        public float DurationSeconds { get; }

        public float Range { get; }
    }

    public sealed class StreakAirstrikeSpec
    {
        public const int DefaultShellCount = 5;

        public const float DefaultIntervalSeconds = 0.35f;

        public const float DefaultSpread = 6f;

        // 敵の最大体力 (100) を中心で超え、1 発の直撃で倒せる。
        public const int DefaultMaxDamage = 150;

        public const float DefaultRadius = 6f;

        public const float DefaultFullDamageRadius = 2f;

        public const int DefaultStructureDamageAtCenter = 100;

        public static readonly StreakAirstrikeSpec Default = new StreakAirstrikeSpec();

        public StreakAirstrikeSpec(
            int shellCount = DefaultShellCount,
            float intervalSeconds = DefaultIntervalSeconds,
            float spread = DefaultSpread,
            ExplosionSpec explosion = null)
        {
            ShellCount = SpecGuard.AtLeast(shellCount, 1, nameof(shellCount));
            IntervalSeconds = SpecGuard.NonNegativeFinite(intervalSeconds, nameof(intervalSeconds));
            Spread = SpecGuard.NonNegativeFinite(spread, nameof(spread));
            Explosion = explosion ?? DefaultExplosion();
        }

        public int ShellCount { get; }

        public float IntervalSeconds { get; }

        public float Spread { get; }

        public ExplosionSpec Explosion { get; }

        public static ExplosionSpec DefaultExplosion()
        {
            return new ExplosionSpec(
                new ExplosionDamage(DefaultMaxDamage, DefaultRadius, DefaultFullDamageRadius), DefaultStructureDamageAtCenter);
        }
    }
}
