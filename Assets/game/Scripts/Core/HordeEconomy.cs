using System;

namespace Shooter.Core
{
    // 試合の型 (オフラインの ModeSession・専用サーバーの TeamDeathmatchSession) に触れずに通貨だけを持つのは、
    // オフラインの Horde と専用サーバーの HordeRules が同じ額と同じ判定で買い物をさせるため。
    public sealed class HordeEconomy
    {
        private readonly WaveSchedule _schedule;

        public HordeEconomy(WaveSchedule schedule, HordeSpec spec)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Currency = spec.StartingCurrency;
        }

        public HordeSpec Spec { get; }

        public int Currency { get; private set; }

        public int EnemiesKilled { get; private set; }

        public bool IsBetweenWaves => HordeRespawn.IsBetween(_schedule.Phase);

        // 最初の波の前に倒した敵 (波の敵でない) には払わない。
        public bool TryEarn(out int reward)
        {
            if (_schedule.WaveNumber < 1)
            {
                reward = 0;
                return false;
            }

            reward = Spec.KillRewardAt(_schedule.WaveNumber);
            Currency = (int)Math.Min(int.MaxValue, (long)Currency + reward);
            EnemiesKilled++;
            return true;
        }

        public HordePurchaseVerdict Check(bool playing, int cost)
        {
            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "cost must not be negative");
            }

            if (!playing)
            {
                return HordePurchaseVerdict.NotPlaying;
            }

            if (Spec.PurchasesOnlyBetweenWaves && !IsBetweenWaves)
            {
                return HordePurchaseVerdict.WaveInProgress;
            }

            return cost > Currency ? HordePurchaseVerdict.NotEnoughCurrency : HordePurchaseVerdict.Ok;
        }

        public HordePurchaseVerdict TryPay(bool playing, int cost)
        {
            var verdict = Check(playing, cost);
            if (verdict == HordePurchaseVerdict.Ok)
            {
                Currency -= cost;
            }

            return verdict;
        }
    }
}
