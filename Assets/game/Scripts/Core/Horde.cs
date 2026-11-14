using System;

namespace Fps.Core
{
    /// <summary>
    /// ホード (PvE の防衛)。守る側 1 チームが <see cref="WaveSchedule"/> の波を耐える。
    /// <see cref="Session"/> はチーム分けが守る側だけ (<see cref="SingleTeam"/>) で、波の敵は参加者にしない (外から <see cref="ReportEnemyKilled"/> で伝える)。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>最後の波を全滅させたら守る側の勝ち、守る側が全員倒れたら攻める側の勝ち (<see cref="HordeWin"/>)。時間では終わらない。</item>
    /// <item>倒れた味方は波の間に戻る (<see cref="HordeRespawn"/>)。</item>
    /// <item>敵を倒すと、そのときの波の番号で決まる通貨 (<see cref="HordeSpec.KillRewardAt"/>) が守る側の共有の財布に入り、
    /// 守る側の得点 (倒した敵の数) が 1 増える。</item>
    /// <item>通貨は防衛物を直す・置くのに払う (<see cref="TryPurchase"/>)。設定によっては波の間だけ払える。</item>
    /// <item>波の予定は外 (Runtime の WaveSpawner、テストでは直接) で始めて進める。ここでは読むだけ。</item>
    /// <item><see cref="ModeSession.Reset"/> には対応しない (波の予定を戻せないため)。やり直すときは作り直す。</item>
    /// </list>
    /// </remarks>
    public sealed class Horde
    {
        private readonly HordeRespawn _respawn;

        /// <param name="schedule">波の予定。開始前でも開始後でもよい。</param>
        /// <param name="spec">陣営と通貨の設定。</param>
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

        /// <summary>守る側の参加者と勝ち負け。参加者を入れて始めるのは外。</summary>
        public ModeSession Session { get; }

        public HordeRespawn Respawn => _respawn;

        /// <summary>守る側の今の通貨。</summary>
        public int Currency { get; private set; }

        /// <summary>試合中に倒した敵の数。</summary>
        public int EnemiesKilled { get; private set; }

        /// <summary>波の間 (休憩中か、最初の波の前) なら true。</summary>
        public bool IsBetweenWaves => _respawn.IsBetweenWaves;

        /// <summary>試合が終わっていて、守る側が勝っていれば true。</summary>
        public bool IsVictory => Session.State == GameModeState.Ended && Session.Winner == Spec.Defenders;

        /// <summary>
        /// 波の敵を 1 体倒したことを伝え、入った通貨を返す。通貨は今の波の番号で決める (休憩中は全滅させた波の番号)。
        /// 試合中でないか、波が始まっていなければ何もせず 0 を返す。
        /// </summary>
        public int ReportEnemyKilled()
        {
            if (Session.State != GameModeState.Playing || Schedule.WaveNumber < 1)
            {
                return 0;
            }

            var reward = Spec.KillRewardAt(Schedule.WaveNumber);
            Currency = (int)Math.Min(int.MaxValue, (long)Currency + reward);
            EnemiesKilled++;
            Session.AddScore(Spec.Defenders, 1d);
            return reward;
        }

        /// <summary>cost を払えるかどうか。払わない。cost は 0 以上。</summary>
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

        /// <summary>cost を払えれば払って <see cref="HordePurchaseVerdict.Ok"/>、払えなければ通貨を変えずに理由を返す。</summary>
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
