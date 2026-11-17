using System;

namespace Shooter.Core
{
    // ModeSession.Reset でのやり直しには対応せず作り直させるのは、波の予定を戻せないため。
    public sealed class Horde
    {
        private readonly HordeRespawn _respawn;

        public Horde(WaveSchedule schedule, HordeSpec spec)
        {
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _respawn = new HordeRespawn(schedule);
            Session = new ModeSession(
                new SingleTeam(spec.Defenders),
                float.PositiveInfinity,
                Array.Empty<IScoreRule>(),
                _respawn,
                new IWinCondition[] { new HordeWin(schedule, spec.Defenders, spec.Attackers) });
            Currency = spec.StartingCurrency;
        }

        public WaveSchedule Schedule { get; }

        public HordeSpec Spec { get; }

        public ModeSession Session { get; }

        public HordeRespawn Respawn => _respawn;

        public int Currency { get; private set; }

        public int EnemiesKilled { get; private set; }

        public bool IsBetweenWaves => _respawn.IsBetweenWaves;

        public bool IsVictory => Session.State == GameModeState.Ended && Session.Winner == Spec.Defenders;

        public int ReportEnemyKilled()
        {
            if (Session.State != GameModeState.Playing || Schedule.WaveNumber < 1)
            {
                return 0;
            }

            var reward = Spec.KillRewardAt(Schedule.WaveNumber);
            Currency = (int)Math.Min(int.MaxValue, (long)Currency + reward);
            EnemiesKilled++;
            Session.AddScore(Spec.Defenders, Spec.KillScore);
            return reward;
        }

        public HordePurchaseVerdict CheckPurchase(int cost)
        {
            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "cost must not be negative");
            }

            if (Session.State != GameModeState.Playing)
            {
                return HordePurchaseVerdict.NotPlaying;
            }

            if (Spec.PurchasesOnlyBetweenWaves && !IsBetweenWaves)
            {
                return HordePurchaseVerdict.WaveInProgress;
            }

            return cost > Currency ? HordePurchaseVerdict.NotEnoughCurrency : HordePurchaseVerdict.Ok;
        }

        public HordePurchaseVerdict TryPurchase(int cost)
        {
            var verdict = CheckPurchase(cost);
            if (verdict == HordePurchaseVerdict.Ok)
            {
                Currency -= cost;
            }

            return verdict;
        }
    }
}
