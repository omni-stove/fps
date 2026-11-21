using System;

namespace Shooter.Core
{
    // ModeSession.Reset でのやり直しには対応せず作り直させるのは、波の予定を戻せないため。
    public sealed class Horde
    {
        private readonly HordeRespawn _respawn;
        private readonly HordeEconomy _economy;

        public Horde(WaveSchedule schedule, HordeSpec spec)
        {
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _respawn = new HordeRespawn(schedule);
            _economy = new HordeEconomy(schedule, spec);
            Session = new ModeSession(
                new SingleTeam(spec.Defenders),
                float.PositiveInfinity,
                Array.Empty<IScoreRule>(),
                _respawn,
                new IWinCondition[] { new HordeWin(schedule, spec.Defenders, spec.Attackers) });
        }

        public WaveSchedule Schedule { get; }

        public HordeSpec Spec { get; }

        public ModeSession Session { get; }

        public HordeRespawn Respawn => _respawn;

        public int Currency => _economy.Currency;

        public int EnemiesKilled => _economy.EnemiesKilled;

        public bool IsBetweenWaves => _respawn.IsBetweenWaves;

        public bool IsVictory => Session.State == GameModeState.Ended && Session.Winner == Spec.Defenders;

        public int ReportEnemyKilled()
        {
            if (Session.State != GameModeState.Playing || !_economy.TryEarn(out var reward))
            {
                return 0;
            }

            Session.AddScore(Spec.Defenders, Spec.KillScore);
            return reward;
        }

        public HordePurchaseVerdict CheckPurchase(int cost)
        {
            return _economy.Check(Session.State == GameModeState.Playing, cost);
        }

        public HordePurchaseVerdict TryPurchase(int cost)
        {
            return _economy.TryPay(Session.State == GameModeState.Playing, cost);
        }
    }
}
