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

        public StreakSpec(
            IReadOnlyList<StreakReward> rewards = null,
            StreakUavSpec uav = null,
            StreakAirstrikeSpec airstrike = null,
            StreakTargetingSpec targeting = null)
        {
            Rewards = rewards == null ? DefaultRewards : CheckRewards(rewards);
            Uav = uav ?? StreakUavSpec.Default;
            Airstrike = airstrike ?? StreakAirstrikeSpec.Default;
            Targeting = targeting ?? StreakTargetingSpec.Default;
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

        public StreakTargetingSpec Targeting { get; }

        public static bool NeedsTarget(string rewardId)
        {
            return string.Equals(rewardId, AirstrikeRewardId, StringComparison.Ordinal);
        }

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

        // 線の上の各点からずらす半径。線に沿って並べるので、円に散らしていた頃より小さくする。
        public const float DefaultSpread = 1.5f;

        // 5 発を 6 m おきに並べ、爆発の半径 (6 m) が隙間なく重なる長さ。
        public const float DefaultLineLength = 24f;

        // 屋根や橋の上で止まるよう、この高さから真下へ落ちる先を探す。
        public const float DefaultDropHeight = 40f;

        // 敵の最大体力 (100) を中心で超え、1 発の直撃で倒せる。
        public const int DefaultMaxDamage = 150;

        public const float DefaultRadius = 6f;

        public const float DefaultFullDamageRadius = 2f;

        public const int DefaultStructureDamageAtCenter = 100;

        public const float DefaultRange = 100f;

        public static readonly StreakAirstrikeSpec Default = new StreakAirstrikeSpec();

        public StreakAirstrikeSpec(
            int shellCount = DefaultShellCount,
            float intervalSeconds = DefaultIntervalSeconds,
            float spread = DefaultSpread,
            ExplosionSpec explosion = null,
            float range = DefaultRange,
            float lineLength = DefaultLineLength,
            float dropHeight = DefaultDropHeight)
        {
            ShellCount = SpecGuard.AtLeast(shellCount, 1, nameof(shellCount));
            IntervalSeconds = SpecGuard.NonNegativeFinite(intervalSeconds, nameof(intervalSeconds));
            Spread = SpecGuard.NonNegativeFinite(spread, nameof(spread));
            Explosion = explosion ?? DefaultExplosion();
            Range = SpecGuard.PositiveFinite(range, nameof(range));
            LineLength = SpecGuard.NonNegativeFinite(lineLength, nameof(lineLength));
            DropHeight = SpecGuard.PositiveFinite(dropHeight, nameof(dropHeight));
        }

        public int ShellCount { get; }

        public float IntervalSeconds { get; }

        public float Spread { get; }

        public ExplosionSpec Explosion { get; }

        public float Range { get; }

        public float LineLength { get; }

        public float DropHeight { get; }

        public static ExplosionSpec DefaultExplosion()
        {
            return new ExplosionSpec(
                new ExplosionDamage(DefaultMaxDamage, DefaultRadius, DefaultFullDamageRadius), DefaultStructureDamageAtCenter);
        }
    }

    public sealed class StreakTargetingSpec
    {
        public const float DefaultCursorSpeed = 40f;

        public const float DefaultTurnDegreesPerSecond = 180f;

        public const float DefaultViewHeight = 150f;

        public static readonly StreakTargetingSpec Default = new StreakTargetingSpec();

        public StreakTargetingSpec(
            float cursorSpeed = DefaultCursorSpeed,
            float turnDegreesPerSecond = DefaultTurnDegreesPerSecond,
            float viewHeight = DefaultViewHeight)
        {
            CursorSpeed = SpecGuard.PositiveFinite(cursorSpeed, nameof(cursorSpeed));
            TurnDegreesPerSecond = SpecGuard.PositiveFinite(turnDegreesPerSecond, nameof(turnDegreesPerSecond));
            ViewHeight = SpecGuard.PositiveFinite(viewHeight, nameof(viewHeight));
        }

        public float CursorSpeed { get; }

        // ボタンで回す速さにだけ使う。視点の入力は、視点を回すときと同じ感度の設定で回す。
        public float TurnDegreesPerSecond { get; }

        public float ViewHeight { get; }
    }
}
