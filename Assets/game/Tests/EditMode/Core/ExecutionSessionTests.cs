using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ExecutionSessionTests
    {
        private const int _alphaOne = 10;
        private const int _alphaTwo = 12;
        private const int _bravoOne = 11;
        private const int _bravoTwo = 13;
        private const float _executeSeconds = 1.5f;
        private const float _roundSeconds = 60f;

        // 出血 (ActionRules.DefaultDownedBleedoutSeconds = 15 秒) より短い制限時間。
        private const float _shortRoundSeconds = 10f;

        private static readonly string[] _expectedExecuteEvents =
        {
            "downed 11 by 10", "downed 13 by 10", "executed 11 by 10", "executed 13 by 12",
        };

        private static readonly string[] _expectedReviveEvents = { "downed 10 by 11", "revived 10 by 12" };

        private static readonly string[] _expectedBleedEvents = { "downed 11 by 10", "bledout 11 credit 10" };

        private static readonly string[] _expectedOrphanBleedEvents = { "downed 11 by 10", "bledout 11 credit -" };

        private static readonly string[] _expectedDownedOnly = { "downed 11 by 10" };

        private static readonly int[] _expectedDownedIds = { _bravoOne, _bravoTwo };

        [Test]
        public void DowningEveryEnemyDoesNotEndTheRound()
        {
            var execution = Create();
            execution.Start();

            Assert.That(execution.ReportLethal(_bravoOne, _alphaOne), Is.EqualTo(ExecutionHit.Downed));
            Assert.That(execution.ReportLethal(_bravoTwo, _alphaOne), Is.EqualTo(ExecutionHit.Downed));
            execution.Tick(1f);

            Assert.That(execution.Round.RoundNumber, Is.EqualTo(1));
            Assert.That(execution.Round.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(execution.GetDownedIds(), Is.EqualTo(_expectedDownedIds));
            Assert.That(execution.CountRemaining(Side.Bravo), Is.EqualTo(2));
            Assert.That(execution.CountDowned(Side.Bravo), Is.EqualTo(2));
            Assert.That(execution.CountStanding(Side.Bravo), Is.Zero);
            Assert.That(execution.Mode.IsDown(_bravoOne), Is.False, "a downed player has not left the round");
        }

        [Test]
        public void ExecutingEveryEnemyWinsTheRound()
        {
            var execution = Create(preparationSeconds: 3f);
            var log = Record(execution);
            execution.Start();
            execution.Tick(3f);
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.ReportLethal(_bravoTwo, _alphaOne);

            Assert.That(execution.Interact(_alphaOne, _bravoOne, 1f), Is.EqualTo(ExecutionStep.Executing));
            Assert.That(execution.GetExecuteProgress(_bravoOne), Is.EqualTo(1f / _executeSeconds).Within(1e-5f));
            execution.Tick(0.1f);
            Assert.That(execution.Interact(_alphaOne, _bravoOne, 0.5f), Is.EqualTo(ExecutionStep.Executed));

            Assert.That(execution.Mode.IsDown(_bravoOne), Is.True);
            Assert.That(execution.IsDowned(_bravoOne), Is.False);
            Assert.That(execution.Mode.GetKills(_alphaOne), Is.EqualTo(1));
            Assert.That(execution.CountRemaining(Side.Bravo), Is.EqualTo(1));

            Assert.That(execution.Interact(_alphaTwo, _bravoTwo, _executeSeconds), Is.EqualTo(ExecutionStep.Executed));
            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.Zero, "the round ends on the next tick, like any elimination");

            execution.Tick(0f);

            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(execution.LastRoundWinner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(execution.HasFinishedRound, Is.True);
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(2));
            Assert.That(execution.Round.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(execution.Mode.IsDown(_bravoOne), Is.False, "everyone is back for the next round");
            Assert.That(log, Is.EqualTo(_expectedExecuteEvents));
        }

        [Test]
        public void ReachingWinsToWinEndsTheMatch()
        {
            var execution = Create(winsToWin: 2, preparationSeconds: 0f);
            Side? matchWinner = null;
            execution.Round.Ended += side => matchWinner = side;
            execution.Start();

            for (var round = 0; round < 2; round++)
            {
                execution.ReportLethal(_bravoOne, _alphaOne);
                execution.ReportLethal(_bravoTwo, _alphaOne);
                execution.Interact(_alphaOne, _bravoOne, _executeSeconds);
                execution.Interact(_alphaTwo, _bravoTwo, _executeSeconds);
                execution.Tick(0f);
            }

            Assert.That(execution.Round.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(execution.Round.Winner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(matchWinner, Is.EqualTo((Side?)Side.Alpha));
            Assert.That(execution.IsLive, Is.False);
            Assert.That(execution.ReportLethal(_alphaOne, _bravoOne), Is.EqualTo(ExecutionHit.Ignored), "nothing counts after the match");
        }

        [Test]
        public void BleedingOutCreditsTheEnemyWhoDownedThePlayer()
        {
            var execution = Create();
            var log = Record(execution);
            execution.Start();
            execution.ReportLethal(_bravoOne, _alphaOne);

            execution.Tick(ActionRules.DefaultDownedBleedoutSeconds - 1f);
            Assert.That(execution.IsDowned(_bravoOne), Is.True);
            Assert.That(execution.GetBleedoutRemaining(_bravoOne), Is.EqualTo(1f).Within(1e-4f));

            execution.Tick(1f);

            Assert.That(execution.IsDowned(_bravoOne), Is.False);
            Assert.That(execution.Mode.IsDown(_bravoOne), Is.True);
            Assert.That(execution.Mode.GetKills(_alphaOne), Is.EqualTo(1));
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(1), "bravo still has a player");
            Assert.That(log, Is.EqualTo(_expectedBleedEvents));
        }

        [Test]
        public void BleedingOutTheLastEnemyWinsTheRound()
        {
            var execution = Create();
            execution.Start();
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.ReportLethal(_bravoTwo, _alphaTwo);

            execution.Tick(ActionRules.DefaultDownedBleedoutSeconds);

            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(execution.Round.RoundNumber, Is.EqualTo(2));
            Assert.That(execution.GetDownedIds(), Is.Empty);
        }

        [Test]
        public void AnAllyRevivesADownedPlayerWithPartOfTheirHealth()
        {
            var execution = Create();
            var log = Record(execution);
            execution.Start();
            execution.ReportLethal(_alphaOne, _bravoOne);

            Assert.That(execution.Interact(_alphaTwo, _alphaOne, 2f), Is.EqualTo(ExecutionStep.Reviving));
            Assert.That(execution.GetReviveProgress(_alphaOne), Is.EqualTo(2f / ActionRules.DefaultReviveSeconds).Within(1e-5f));
            execution.Tick(0.1f);
            Assert.That(execution.Interact(_alphaTwo, _alphaOne, 1f), Is.EqualTo(ExecutionStep.Revived));

            Assert.That(execution.IsDowned(_alphaOne), Is.False);
            Assert.That(execution.Mode.IsDown(_alphaOne), Is.False);
            Assert.That(execution.CountStanding(Side.Alpha), Is.EqualTo(2));
            Assert.That(execution.RevivedHealth(100), Is.EqualTo(30));
            Assert.That(log, Is.EqualTo(_expectedReviveEvents));
        }

        [Test]
        public void LettingGoStartsTheHoldOver()
        {
            var execution = Create();
            execution.Start();
            execution.ReportLethal(_alphaOne, _bravoOne);
            execution.ReportLethal(_bravoTwo, _alphaTwo);

            execution.Interact(_alphaTwo, _alphaOne, 2f);
            execution.Interact(_alphaTwo, _bravoTwo, 1f);
            execution.Tick(0.1f);
            execution.Tick(0.1f);

            Assert.That(execution.GetReviveProgress(_alphaOne), Is.Zero);
            Assert.That(execution.GetExecuteProgress(_bravoTwo), Is.Zero);
            Assert.That(execution.Interact(_alphaTwo, _alphaOne, 2f), Is.EqualTo(ExecutionStep.Reviving));
            Assert.That(execution.Interact(_alphaTwo, _bravoTwo, 1f), Is.EqualTo(ExecutionStep.Executing));
        }

        [Test]
        public void OnlyOneEnemyCanExecuteTheSamePlayerInATick()
        {
            var execution = Create();
            execution.Start();
            execution.ReportLethal(_alphaOne, _bravoOne);

            Assert.That(execution.Interact(_bravoOne, _alphaOne, 0.5f), Is.EqualTo(ExecutionStep.Executing));
            Assert.That(execution.Interact(_bravoTwo, _alphaOne, 0.5f), Is.EqualTo(ExecutionStep.Rejected));

            execution.Tick(0.1f);
            execution.Tick(0.1f);

            // 前の人が手を離したら、次の人は 0 から数える。
            Assert.That(execution.Interact(_bravoTwo, _alphaOne, 1f), Is.EqualTo(ExecutionStep.Executing));
            Assert.That(execution.GetExecuteProgress(_alphaOne), Is.EqualTo(1f / _executeSeconds).Within(1e-5f));
        }

        [Test]
        public void AnExecutionBeatsAReviveInProgress()
        {
            var execution = Create();
            execution.Start();
            execution.ReportLethal(_alphaOne, _bravoOne);

            Assert.That(execution.Interact(_alphaTwo, _alphaOne, 1f), Is.EqualTo(ExecutionStep.Reviving));
            Assert.That(execution.Interact(_bravoOne, _alphaOne, _executeSeconds), Is.EqualTo(ExecutionStep.Executed));

            Assert.That(execution.Interact(_alphaTwo, _alphaOne, 2f), Is.EqualTo(ExecutionStep.Rejected));
            Assert.That(execution.Mode.IsDown(_alphaOne), Is.True);
        }

        [Test]
        public void InteractRejectsWhatCannotBeHeld()
        {
            var execution = Create(preparationSeconds: 5f);
            execution.Start();

            Assert.That(execution.ReportLethal(_alphaOne, _bravoOne), Is.EqualTo(ExecutionHit.Ignored), "nothing counts while preparing");
            execution.Tick(5f);

            Assert.That(execution.Interact(_alphaTwo, _alphaOne, 1f), Is.EqualTo(ExecutionStep.Rejected), "the target is not downed");

            execution.ReportLethal(_alphaOne, _bravoOne);
            Assert.That(execution.ReportLethal(_alphaOne, _bravoTwo), Is.EqualTo(ExecutionHit.Ignored), "already downed");
            Assert.That(execution.Interact(_alphaOne, _alphaOne, 1f), Is.EqualTo(ExecutionStep.Rejected), "not on oneself");

            execution.ReportLethal(_bravoOne, _alphaTwo);
            Assert.That(execution.Interact(_bravoOne, _alphaOne, 1f), Is.EqualTo(ExecutionStep.Rejected), "a downed player cannot act");

            Assert.Throws<ArgumentOutOfRangeException>(() => execution.Interact(99, _alphaOne, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => execution.Interact(_bravoTwo, _alphaOne, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => execution.ReportLethal(99, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => execution.ReportLethal(_alphaOne, 99));
        }

        [Test]
        public void ATimeUpGoesToTheSideWithMoreStandingPlayers()
        {
            // 出血で倒れる前に時間を切らせ、ダウン中の人を立っている人に数えないことを確かめる。
            var execution = Create(roundSeconds: _shortRoundSeconds);
            var log = Record(execution);
            execution.Start();
            execution.ReportLethal(_bravoOne, _alphaOne);

            execution.Tick(_shortRoundSeconds);

            Assert.That(log, Is.EqualTo(_expectedDownedOnly), "the downed player did not bleed out");
            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(execution.LastRoundWinner, Is.EqualTo((Side?)Side.Alpha));
        }

        [Test]
        public void ATimeUpWithEqualStandingPlayersIsADraw()
        {
            var execution = Create(roundSeconds: _shortRoundSeconds);
            execution.Start();
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.ReportLethal(_alphaOne, _bravoTwo);

            execution.Tick(_shortRoundSeconds);

            Assert.That(execution.HasFinishedRound, Is.True);
            Assert.That(execution.LastRoundWinner, Is.Null);
            Assert.That(execution.Round.GetRoundWins(Side.Alpha), Is.Zero);
            Assert.That(execution.Round.GetRoundWins(Side.Bravo), Is.Zero);
        }

        [Test]
        public void TheNextRoundForgetsTheDowned()
        {
            var execution = Create(preparationSeconds: 2f);
            execution.Start();
            execution.Tick(2f);
            execution.ReportLethal(_alphaOne, _bravoOne);
            execution.ReportLethal(_bravoOne, _alphaTwo);
            execution.ReportLethal(_bravoTwo, _alphaTwo);
            execution.Interact(_alphaTwo, _bravoTwo, _executeSeconds);
            execution.Interact(_alphaTwo, _bravoOne, _executeSeconds);

            execution.Tick(0f);

            Assert.That(execution.Round.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(execution.IsDowned(_alphaOne), Is.False);
            Assert.That(execution.GetDownedIds(), Is.Empty);
            Assert.That(execution.CountRemaining(Side.Bravo), Is.EqualTo(2));
        }

        [Test]
        public void WithoutDownsALethalHitKillsAtOnce()
        {
            var execution = Create(downed: Downed.Off);
            execution.Start();

            Assert.That(execution.UsesDowned, Is.False);
            Assert.That(execution.ReportLethal(_bravoOne, _alphaOne), Is.EqualTo(ExecutionHit.Killed));
            Assert.That(execution.Mode.IsDown(_bravoOne), Is.True);
            Assert.That(execution.Mode.GetKills(_alphaOne), Is.EqualTo(1));
            Assert.That(execution.Interact(_alphaOne, _bravoOne, 1f), Is.EqualTo(ExecutionStep.Rejected));
        }

        [Test]
        public void ALeavingDownerGetsNoBleedoutCredit()
        {
            var execution = Create();
            var log = Record(execution);
            execution.Start();
            execution.ReportLethal(_bravoOne, _alphaOne);

            execution.RemovePlayer(_alphaOne);
            execution.Tick(ActionRules.DefaultDownedBleedoutSeconds);

            Assert.That(execution.Mode.IsDown(_bravoOne), Is.True);
            Assert.That(execution.Mode.GetDeaths(_bravoOne), Is.EqualTo(1));
            Assert.That(log, Is.EqualTo(_expectedOrphanBleedEvents));
        }

        [Test]
        public void ResetGoesBackToBeforeTheFirstRound()
        {
            var execution = Create();
            execution.Start();
            execution.ReportLethal(_bravoOne, _alphaOne);
            execution.Tick(_roundSeconds);

            execution.Reset();

            Assert.That(execution.Round.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(execution.Round.RoundNumber, Is.Zero);
            Assert.That(execution.HasFinishedRound, Is.False);
            Assert.That(execution.LastRoundWinner, Is.Null);
            Assert.That(execution.GetDownedIds(), Is.Empty);
        }

        [Test]
        public void TheConstructorRejectsInvalidValues()
        {
            var actions = ActionRules.Default.WithDowned(Downed.Revive);

            Assert.Throws<ArgumentNullException>(() => new ExecutionSession(1, 0f, _roundSeconds, null, _executeSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSession(1, 0f, _roundSeconds, actions, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSession(1, 0f, _roundSeconds, actions, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSession(0, 0f, _roundSeconds, actions, _executeSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionSession(1, 0f, 0f, actions, _executeSeconds));
        }

        private static ExecutionSession Create(
            int winsToWin = 3, float preparationSeconds = 0f, Downed downed = Downed.Revive, float roundSeconds = _roundSeconds)
        {
            var execution = new ExecutionSession(
                winsToWin, preparationSeconds, roundSeconds, ActionRules.Default.WithDowned(downed), _executeSeconds);
            execution.Mode.AddPlayer(_alphaOne, Side.Alpha);
            execution.Mode.AddPlayer(_bravoOne, Side.Bravo);
            execution.Mode.AddPlayer(_alphaTwo, Side.Alpha);
            execution.Mode.AddPlayer(_bravoTwo, Side.Bravo);
            return execution;
        }

        private static List<string> Record(ExecutionSession execution)
        {
            var log = new List<string>();
            execution.PlayerDowned += (victim, downer) => log.Add("downed " + victim + " by " + (downer.HasValue ? downer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-"));
            execution.PlayerRevived += (reviver, target) => log.Add("revived " + target + " by " + reviver);
            execution.PlayerExecuted += (executor, victim) => log.Add("executed " + victim + " by " + executor);
            execution.PlayerBledOut += (victim, credit) => log.Add("bledout " + victim + " credit " + (credit.HasValue ? credit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-"));
            return log;
        }
    }
}
