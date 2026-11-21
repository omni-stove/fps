using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // Execution の目的 (ExecutionRules) で、ダウンした敵に近接のボタンでとどめを刺せ (テイクダウンと同じ ServerMelee の仕組み)、
    // 全員が倒れた側がラウンドを落とすことを確かめる。目的は既定の選び方 (ServerObjectiveFactory) で作る。
    // 1 と 3 が Alpha、2 と 4 が Bravo。2 を 1 の 1.5 m 東 (体の表面まで 1 m) に置き、3 と 4 は離しておく
    public sealed partial class ServerMeleeTests
    {
        private const float _executeSeconds = 0.5f;
        private const float _preparationSeconds = 0.1f;

        private static readonly int _executeTicks = (int)MathF.Ceiling(_executeSeconds / _tickSeconds) + 2;

        // 確認方法の筋書き: ダウンした敵は撃っても倒れず、近接のボタンでとどめを刺すと倒れ、倒した人のキルになる
        [Test]
        public void ADownedEnemyIsExecutedWithTheMeleeButtonAndNotByShots()
        {
            StartExecutionWithFour();
            var kills = new List<KillFeedEntry>();
            _combat.PlayerKilled += kills.Add;

            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed), _log.ToString());
            Assert.That(_rules.Session.IsDowned(2), Is.True);
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Ignored), "a shot does not finish a downed player");
            Assert.That(
                _combat.DealDamage(1, 2, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null),
                Is.EqualTo(DamageOutcome.Ignored),
                "nor does other damage");
            Assert.That(_rules.Session.IsDowned(2), Is.True);
            Assert.That(kills, Is.Empty);

            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);
            Press(1);

            Assert.That(_melee.IsPerformingTakedown(1), Is.True, _log.ToString());
            Assert.That(_melee.TryGetTakedownTarget(1, out var target), Is.True);
            Assert.That(target, Is.EqualTo(2));
            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] started id=1 target=2 move=" + ContextMeleeMove.ExecuteId));
            Assert.That(_melee.IsSwinging(1), Is.False, "the execution replaces the ordinary swing");

            Hold(1, InputButtons.None, _executeTicks - 10);
            Assert.That(_rules.Session.IsDowned(2), Is.True, "no finish before the move resolves");

            Hold(1, InputButtons.None, 10);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            Assert.That(_rules.Session.IsDowned(2), Is.False);
            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
            var log = _log.ToString();
            Assert.That(log, Does.Contain(ContextMeleeLog.ResolvedPrefix("1", "2", ContextMeleeMove.ExecuteId) + "outcome=Kill dmg="));
            Assert.That(log, Does.Contain(" killed=true"));
            Assert.That(kills, Has.Count.EqualTo(1));
            Assert.That(kills[0].KillerId, Is.EqualTo(1));
            Assert.That(kills[0].VictimId, Is.EqualTo(2));
        }

        // 確認方法の筋書き: とどめを刺されて残りが倒れた側がラウンドを落とし、勝ち数に届けば試合も落とす
        [Test]
        public void TheSideWhoseEveryoneFellLosesTheRoundAndTheMatch()
        {
            StartExecutionWithFour(roundWinsToWin: 1);
            DownAndExecute(1, 2);
            Assert.That(_rules.Session.IsAlive(4), Is.True);
            Assert.That(Rules().Rounds.Phase, Is.EqualTo(RoundPhase.Live), "Bravo still has a player standing");

            WaitOutTheSwing(1);
            DownAndExecute(1, 4);
            Tick();

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended), _log.ToString());
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Objective, Is.EqualTo(MatchObjective.Execution));
            Assert.That(snapshot.HasWinner, Is.True);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(1));
            Assert.That(snapshot.BravoPoints, Is.Zero);
            Assert.That(snapshot.HasRound, Is.True);
            Assert.That(snapshot.Round, Is.EqualTo(RoundPhase.Ended));
            Assert.That(snapshot.HasLastRound, Is.True);
            Assert.That(snapshot.HasLastRoundWinner, Is.True);
            Assert.That(snapshot.LastRoundWinner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.DefendersEliminated), "Bravo was wiped out");
            Assert.That(_log.ToString(), Does.Contain(MatchLog.RoundEnded(1, Team.Alpha, 1, 0)));
            Assert.That(_log.ToString(), Does.Contain("[Execution] state round=1 phase=Ended wins=1-0"));
        }

        [Test]
        public void TheNextRoundStartsWithEveryoneStandingAndShowsThePreviousWinner()
        {
            StartExecutionWithFour(roundWinsToWin: 2);
            DownAndExecute(1, 2);
            WaitOutTheSwing(1);
            DownAndExecute(1, 4);
            Tick();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing), "one round win is not enough");

            Step(_preparationSeconds + 0.1f);

            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.RoundNumber, Is.EqualTo(2));
            Assert.That(snapshot.RoundsToWin, Is.EqualTo(2));
            Assert.That(snapshot.AlphaPoints, Is.EqualTo(1));
            Assert.That(snapshot.HasLastRoundWinner, Is.True);
            Assert.That(snapshot.LastRoundWinner, Is.EqualTo(Team.Alpha));
            foreach (var id in new[] { 1, 2, 3, 4 })
            {
                Assert.That(_rules.Session.IsAlive(id), Is.True, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Assert.That(_rules.Session.IsDowned(id), Is.False);
            }
        }

        // 時間切れでは、ダウン中の人を数えずに立っている人の多い側が勝つ (オフラインの ExecutionSession と同じ)
        [Test]
        public void AtTimeUpTheSideWithMoreStandingWins()
        {
            StartExecutionWithFour(roundWinsToWin: 1, roundTimeLimitSeconds: 1f);
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));

            Step(1.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended), _log.ToString());
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.TimeUp));
        }

        [Test]
        public void ATeammateReviveStopsTheExecution()
        {
            StartExecutionWithFour();
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);
            Press(1);
            Assert.That(_melee.IsPerformingTakedown(1), Is.True, _log.ToString());

            Assert.That(_rules.Session.TryRevive(4, 2, 60f), Is.True);
            Hold(1, InputButtons.None, _executeTicks);

            Assert.That(_log.ToString(), Does.Contain("[ContextMelee] interrupted id=1 target=2 move=" + ContextMeleeMove.ExecuteId + " reason=TargetLost"));
            Assert.That(_rules.Session.IsAlive(2), Is.True, "the revived player is not finished");
            Assert.That(_rules.Session.IsDowned(2), Is.False);
            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
        }

        [Test]
        public void ATeammateIsNotExecuted()
        {
            StartExecutionWithFour();
            Assert.That(_rules.ApplyDamage(2, 3, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            PlaceFacing(3, -3.5f, 0f, _facingWest);
            Place(2, 5f, 6f);
            Face(1);

            Press(1);

            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("move=" + ContextMeleeMove.ExecuteId));
            Assert.That(_rules.Session.IsDowned(3), Is.True);
        }

        [Test]
        public void ADownedEnemyOutOfTheExecuteRangeIsNotExecuted()
        {
            StartExecutionWithFour();
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));

            // 中心どうし 3.5 m、体の表面まで 3 m で、とどめの距離 2 m より遠い
            PlaceFacing(2, -1.5f, 0f, _facingWest);
            Face(1);
            Press(1);

            Assert.That(_melee.IsPerformingTakedown(1), Is.False);
            Assert.That(_log.ToString(), Does.Not.Contain("move=" + ContextMeleeMove.ExecuteId));
        }

        // とどめの技は目的が出すので、ダウンを使うだけの試合 (目的なし) では選ばれず、ダウン中の人もふつうの近接で削れる
        [Test]
        public void WithoutTheExecutionObjectiveADownedEnemyTakesTheOrdinarySwing()
        {
            var rules = MeleeRules();
            Start(rules.WithActions(rules.Actions.WithDowned(Downed.Revive)));
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            Step(0.2f);
            Place(3, -5f, 6f);
            Place(4, 5f, 6f);
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            var downedHealth = _rules.Session.GetHealth(2);
            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);

            Swing(1);

            Assert.That(_log.ToString(), Does.Not.Contain("move=" + ContextMeleeMove.ExecuteId));
            Assert.That(_log.ToString(), Does.Contain("[Melee] hit id=1 target=2"));
            Assert.That(_rules.Session.GetHealth(2), Is.LessThan(downedHealth), "the downed player is not shielded without the objective");
        }

        // 確認方法の筋書き: 各チームの最後の 1 人も倒れた時点では倒れずにダウンし、とどめを刺されるまでラウンドは続く
        [Test]
        public void TheLastPlayerStandingIsDownedAndTheRoundWaitsForTheExecution()
        {
            StartExecutionWithFour(roundWinsToWin: 1);
            DownAndExecute(1, 2);
            WaitOutTheSwing(1);

            Assert.That(_rules.ApplyDamage(1, 4, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed), "the last player standing is downed, not killed");
            Tick();

            Assert.That(_rules.Session.IsDowned(4), Is.True);
            Assert.That(_rules.Session.IsAlive(4), Is.True);
            Assert.That(Rules().Rounds.Phase, Is.EqualTo(RoundPhase.Live), "the round waits for the execution");
            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_rules.ApplyDamage(3, 4, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Ignored), "a shot still does not finish the last one");

            PlaceFacing(4, -3.5f, 0f, _facingWest);
            Face(1);
            Press(1);
            Hold(1, InputButtons.None, _executeTicks);
            Tick();

            Assert.That(_rules.Session.IsAlive(4), Is.False, _log.ToString());
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
            Assert.That(snapshot.LastRoundEnd, Is.EqualTo(RoundEndReason.DefendersEliminated));
        }

        // オフラインの ExecutionSession と同じく、全員がダウンしてもラウンドは終わらず、出血で倒れきった側が落とす
        [Test]
        public void AWholeSideDownedLosesOnlyWhenItBleedsOut()
        {
            StartExecutionWithFour(roundWinsToWin: 1);

            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(_rules.ApplyDamage(1, 4, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            Tick();

            Assert.That(_rules.Session.IsDowned(2), Is.True);
            Assert.That(_rules.Session.IsDowned(4), Is.True);
            Assert.That(Rules().Rounds.Phase, Is.EqualTo(RoundPhase.Live), "a side whose everyone is downed has not fallen yet");

            Step(ActionRules.DefaultDownedBleedoutSeconds + 0.5f);

            Assert.That(_rules.Session.IsAlive(2), Is.False, _log.ToString());
            Assert.That(_rules.Session.IsAlive(4), Is.False);
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            var snapshot = default(MatchSnapshot);
            _rules.FillMatch(ref snapshot);
            Assert.That(snapshot.Winner, Is.EqualTo(Team.Alpha));
        }

        // とどめの進み具合はサーバーの近接が持つので、刺されている人のダウンの欄に刺している人と百分率を書き足す
        [Test]
        public void TheExecutionProgressIsWrittenOnTheDownedRow()
        {
            StartExecutionWithFour();
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);

            var before = DownedRow(2);
            Assert.That(before.IsDowned, Is.True);
            Assert.That(before.HasExecutor, Is.False, "nobody is executing yet");

            Press(1);
            Hold(1, InputButtons.None, _executeTicks / 2);

            Assert.That(_melee.TryGetExecution(2, out var executorId, out var progress), Is.True, _log.ToString());
            Assert.That(executorId, Is.EqualTo(1));
            Assert.That(progress, Is.GreaterThan(0f).And.LessThan(1f));
            var during = DownedRow(2);
            Assert.That(during.HasExecutor, Is.True);
            Assert.That(during.ExecutorId, Is.EqualTo(1));
            Assert.That(during.ExecutePercent, Is.EqualTo(HudFormat.RevivePercent(progress)));
            Assert.That(during.ExecutePercent, Is.GreaterThan(0).And.LessThan(DownedSync.FullPercent));
            Assert.That(DownedRow(4).HasExecutor, Is.False, "only the executed player carries the progress");

            Hold(1, InputButtons.None, _executeTicks);

            Assert.That(_melee.TryGetExecution(2, out _, out _), Is.False);
            Assert.That(DownedRow(2).HasExecutor, Is.False, "the finished player is no longer downed");
        }

        [Test]
        public void ARevivedTargetLosesTheExecutionProgress()
        {
            StartExecutionWithFour();
            Assert.That(_rules.ApplyDamage(1, 2, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            PlaceFacing(2, -3.5f, 0f, _facingWest);
            Face(1);
            Press(1);
            Assert.That(DownedRow(2).HasExecutor, Is.True, _log.ToString());

            Assert.That(_rules.Session.TryRevive(4, 2, 60f), Is.True);
            Tick();

            Assert.That(_melee.TryGetExecution(2, out _, out _), Is.False);
            Assert.That(DownedRow(2).HasExecutor, Is.False);
            Assert.That(DownedRow(2).IsDowned, Is.False);
        }

        private DownedSync DownedRow(int playerId)
        {
            var snapshot = new SnapshotMessage();
            foreach (var player in _match.Players)
            {
                _rules.FillPlayer(player.PlayerId, ref snapshot.Players.Add());
            }

            Assert.That(_match.TryGetPlayer(playerId, out var receiver), Is.True);
            _melee.Contribute(snapshot, receiver);
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                if (snapshot.Players[i].PlayerId == playerId)
                {
                    return snapshot.Players[i].Downed;
                }
            }

            Assert.Fail("no row for player " + playerId);
            return default;
        }

        // 1 人目のとどめの後、続けて近接のボタンを押しても待ちで断られないよう、振りと待ちの分 (1 秒) 進める
        private void WaitOutTheSwing(int playerId)
        {
            Hold(playerId, InputButtons.None, (int)(1f / _tickSeconds));
        }

        private ExecutionRules Rules()
        {
            return (ExecutionRules)_rules.Objective;
        }

        private void StartExecutionWithFour(int roundWinsToWin = 3, float roundTimeLimitSeconds = 120f)
        {
            var rules = MeleeRules();
            var settings = new NetMatchSettings(
                roundWinsToWin: roundWinsToWin,
                roundPreparationSeconds: _preparationSeconds,
                roundTimeLimitSeconds: roundTimeLimitSeconds,
                execution: new ExecutionSpec(_executeSeconds, ExecutionSpec.DefaultExecuteRange));
            Start(
                rules.WithActions(rules.Actions.WithDowned(Downed.Revive)).WithObjective(MatchObjective.Execution),
                settings: settings);
            Join(1);
            Join(2);
            Join(3);
            Join(4);
            Step(_preparationSeconds + 0.1f);
            Assert.That(Rules().Rounds.Phase, Is.EqualTo(RoundPhase.Live), _log.ToString());
            Assert.That(_rules.Session.GetTeam(3), Is.EqualTo(_rules.Session.GetTeam(1)));
            Assert.That(_rules.Session.GetTeam(4), Is.EqualTo(_rules.Session.GetTeam(2)));
            Assert.That(_rules.Session.GetTeam(2), Is.Not.EqualTo(_rules.Session.GetTeam(1)));
            Place(3, -5f, 6f);
            Place(4, 5f, 6f);
        }

        private void DownAndExecute(int attackerId, int victimId)
        {
            Assert.That(_rules.ApplyDamage(attackerId, victimId, NetMatchSettings.DefaultMaxHealth), Is.EqualTo(DamageOutcome.Downed));
            PlaceFacing(victimId, -3.5f, 0f, _facingWest);
            Face(attackerId);
            Press(attackerId);
            Hold(attackerId, InputButtons.None, _executeTicks);
            // 各チームの最後の 1 人のとどめは、ラウンドの判定が入る分だけ 1 tick 遅れて倒れきる
            Tick();
            // 最後の 1 人が倒れきるとラウンドが終わり、続きのラウンドでは次の準備で全員が出直すので、倒れきったままとは限らない
            var roundOver = Rules().Rounds.Phase != RoundPhase.Live;
            Assert.That(roundOver || !_rules.Session.IsAlive(victimId), Is.True, _log.ToString());
        }
    }
}
