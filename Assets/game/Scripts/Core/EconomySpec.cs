using System;

namespace Shooter.Core
{
    // 人ごとの財布の額だけを持つ。品物の値段は武器や装備ごとに変わるので、ここではなく買える品の一覧のデータに置く。
    public sealed class EconomySpec
    {
        public const int DefaultStartMoney = 800;
        public const int DefaultMaxMoney = 16000;
        public const int DefaultKillReward = 300;
        public const int DefaultRoundWinReward = 3000;
        public const int DefaultRoundLossReward = 1400;
        public const int DefaultLossStreakBonus = 500;
        public const int DefaultMaxLossReward = 3400;

        public static EconomySpec Default { get; } = new EconomySpec();

        public int StartMoney { get; }

        public int MaxMoney { get; }

        public int KillReward { get; }

        public int RoundWinReward { get; }

        public int RoundLossReward { get; }

        public int LossStreakBonus { get; }

        public int MaxLossReward { get; }

        public EconomySpec(
            int startMoney = DefaultStartMoney,
            int maxMoney = DefaultMaxMoney,
            int killReward = DefaultKillReward,
            int roundWinReward = DefaultRoundWinReward,
            int roundLossReward = DefaultRoundLossReward,
            int lossStreakBonus = DefaultLossStreakBonus,
            int maxLossReward = DefaultMaxLossReward)
        {
            RequireNotNegative(startMoney, nameof(startMoney));
            if (maxMoney < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMoney), maxMoney, "maxMoney must be at least 1");
            }

            if (startMoney > maxMoney)
            {
                throw new ArgumentOutOfRangeException(nameof(startMoney), startMoney, "startMoney must not be more than maxMoney (" + maxMoney + ")");
            }

            RequireNotNegative(killReward, nameof(killReward));
            RequireNotNegative(roundWinReward, nameof(roundWinReward));
            RequireNotNegative(roundLossReward, nameof(roundLossReward));
            RequireNotNegative(lossStreakBonus, nameof(lossStreakBonus));
            if (maxLossReward < roundLossReward)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLossReward), maxLossReward, "maxLossReward must not be less than roundLossReward (" + roundLossReward + ")");
            }

            StartMoney = startMoney;
            MaxMoney = maxMoney;
            KillReward = killReward;
            RoundWinReward = roundWinReward;
            RoundLossReward = roundLossReward;
            LossStreakBonus = lossStreakBonus;
            MaxLossReward = maxLossReward;
        }

        public int LossReward(int lossStreak)
        {
            if (lossStreak < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lossStreak), lossStreak, "lossStreak must be at least 1");
            }

            // 長い連敗で int を溢れさせないよう、上限までの段数で先に止める
            var steps = LossStreakBonus == 0 ? 0L : Math.Min(lossStreak - 1L, (MaxLossReward - RoundLossReward) / (long)LossStreakBonus + 1L);
            return (int)Math.Min(RoundLossReward + (steps * LossStreakBonus), MaxLossReward);
        }

        private static void RequireNotNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must not be negative");
            }
        }
    }
}
