using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class EliminationObjectiveTests
    {
        private static readonly string[] _preparedThenLive = { "preparing 1", "live 1" };

        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _events = new List<string>();
        }

        private EliminationObjective Create(int winsToWin = 2, float preparationSeconds = 1f, float roundTimeLimitSeconds = 10f, Func<Team, int> standing = null)
        {
            var objective = new EliminationObjective(winsToWin, preparationSeconds, roundTimeLimitSeconds, standing);
            objective.PreparationStarted += round => _events.Add("preparing " + round);
            objective.RoundStarted += round => _events.Add("live " + round);
            objective.RoundEnded += (round, winner) => _events.Add("ended " + round + " " + (winner?.ToString() ?? "draw"));
            objective.SetPlayer(1, Team.Alpha);
            objective.SetPlayer(2, Team.Bravo);
            return objective;
        }

        [Test]
        public void WaitsForTheMatchBeforeStartingTheFirstRound()
        {
            var objective = Create();

            Assert.That(objective.Tick(false, 5f), Is.False);

            Assert.That(objective.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void PreparesThenGoesLive()
        {
            var objective = Create(preparationSeconds: 1f);

            objective.Tick(true, 0f);
            Assert.That(objective.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(objective.IsPreparing, Is.True);
            Assert.That(objective.RemainingSeconds, Is.EqualTo(1f));

            objective.Tick(true, 1f);

            Assert.That(objective.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(objective.RemainingSeconds, Is.EqualTo(10f));
            Assert.That(_events, Is.EqualTo(_preparedThenLive));
        }

        [Test]
        public void WipingTheOtherTeamWinsTheRoundOnTheNextTick()
        {
            var objective = Create(winsToWin: 2, preparationSeconds: 0f);
            objective.Tick(true, 0f);

            objective.ReportFall(1, 2);
            Assert.That(objective.GetRoundWins(Team.Alpha), Is.Zero);
            objective.Tick(true, 0.1f);

            Assert.That(objective.GetRoundWins(Team.Alpha), Is.EqualTo(1));
            Assert.That(objective.Phase, Is.EqualTo(RoundPhase.Live), "no preparation goes straight to the next round");
            Assert.That(objective.Rounds.RoundNumber, Is.EqualTo(2));
            Assert.That(objective.Mode.IsDown(2), Is.False, "the next round brings the fallen back");
            Assert.That(_events, Does.Contain("ended 1 Alpha"));
        }

        [Test]
        public void TheMatchIsDecidedAtTheWinsToWin()
        {
            var objective = Create(winsToWin: 2, preparationSeconds: 0f);
            objective.Tick(true, 0f);
            objective.ReportFall(2, 1);
            objective.Tick(true, 0.1f);
            objective.ReportFall(2, 1);

            Assert.That(objective.Tick(true, 0.1f), Is.True);

            Assert.That(objective.Phase, Is.EqualTo(RoundPhase.Ended));
            Assert.That(objective.DecideWinner(), Is.EqualTo(Team.Bravo));
            Assert.That(objective.GetRoundWins(Team.Bravo), Is.EqualTo(2));
        }

        [Test]
        public void FallsByAnAllyOrOneselfCountAsDeaths()
        {
            var objective = Create(preparationSeconds: 0f);
            objective.SetPlayer(3, Team.Alpha);
            objective.Tick(true, 0f);

            objective.ReportFall(3, 1);
            objective.ReportFall(null, 3);
            objective.Tick(true, 0.1f);

            Assert.That(objective.Mode.GetKills(3), Is.Zero);
            Assert.That(objective.GetRoundWins(Team.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void FallsOutsideALiveRoundAreIgnored()
        {
            var objective = Create(preparationSeconds: 1f);
            objective.Tick(true, 0f);

            objective.ReportFall(1, 2);
            objective.Tick(true, 1f);
            objective.Tick(true, 0.1f);

            Assert.That(objective.Mode.IsDown(2), Is.False);
            Assert.That(objective.GetRoundWins(Team.Alpha), Is.Zero);
        }

        [Test]
        public void TimeUpGoesToTheTeamWithMoreStanding()
        {
            var objective = Create(preparationSeconds: 0f, roundTimeLimitSeconds: 2f, standing: team => team == Team.Bravo ? 2 : 1);
            objective.Tick(true, 0f);

            objective.Tick(true, 2f);

            Assert.That(objective.GetRoundWins(Team.Bravo), Is.EqualTo(1));
        }

        [Test]
        public void AnEvenTimeUpIsADraw()
        {
            var objective = Create(preparationSeconds: 0f, roundTimeLimitSeconds: 2f);
            objective.Tick(true, 0f);

            objective.Tick(true, 2f);

            Assert.That(_events, Does.Contain("ended 1 draw"));
            Assert.That(objective.GetRoundWins(Team.Alpha), Is.Zero);
            Assert.That(objective.GetRoundWins(Team.Bravo), Is.Zero);
        }

        [Test]
        public void DecidesByRoundWinsBeforeTheEnd()
        {
            var objective = Create(winsToWin: 3, preparationSeconds: 0f);
            Assert.That(objective.DecideWinner(), Is.Null);
            objective.Tick(true, 0f);
            objective.ReportFall(1, 2);
            objective.Tick(true, 0.1f);

            Assert.That(objective.DecideWinner(), Is.EqualTo(Team.Alpha));
        }

        [Test]
        public void ChangingTeamsMovesThePlayer()
        {
            var objective = Create();

            objective.SetPlayer(1, Team.Bravo);
            objective.SetPlayer(1, Team.Bravo);

            Assert.That(objective.Mode.GetSide(1), Is.EqualTo(Side.Bravo));
            Assert.That(objective.PlayerCount, Is.EqualTo(2));
            Assert.That(objective.RemovePlayer(1), Is.True);
            Assert.That(objective.RemovePlayer(1), Is.False);
            Assert.That(objective.HasPlayer(1), Is.False);
        }

        [Test]
        public void ResetGoesBackToWaitingWithNoWins()
        {
            var objective = Create(preparationSeconds: 0f);
            objective.Tick(true, 0f);
            objective.ReportFall(1, 2);
            objective.Tick(true, 0.1f);

            objective.Reset();

            Assert.That(objective.Phase, Is.EqualTo(RoundPhase.Waiting));
            Assert.That(objective.GetRoundWins(Team.Alpha), Is.Zero);
            Assert.That(objective.Rounds.RoundNumber, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsARoundTimeLimitThatIsNotPositiveAndFinite(float seconds)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new EliminationObjective(1, 0f, seconds));

            Assert.That(e.ParamName, Is.EqualTo("roundTimeLimitSeconds"));
        }

        [Test]
        public void RejectsNoWinsToWin()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EliminationObjective(0, 0f, 10f));
        }
    }
}
