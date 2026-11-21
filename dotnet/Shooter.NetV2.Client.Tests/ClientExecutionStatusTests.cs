using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    // 自分 (3) と味方 (5) が Alpha、敵 (4・6) が Bravo
    public sealed class ClientExecutionStatusTests
    {
        private const int _self = 3;
        private const int _ally = 5;
        private const int _enemy = 4;
        private const int _otherEnemy = 6;

        private ClientExecutionStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientExecutionStatus(_self);
        }

        [Test]
        public void TheRoundIsShownInTheSameShapeAsOffline()
        {
            var snapshot = Snapshot(phase: RoundPhase.Live, roundNumber: 2, roundsToWin: 3, alphaWins: 1, bravoWins: 0, remaining: 75);
            Row(snapshot, _enemy).Downed = new DownedSync(true, 12, 40);
            Row(snapshot, _otherEnemy).Health = 0;

            var changed = _status.Apply(snapshot);

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.RemainingSeconds, Is.EqualTo(75f));
            Assert.That(status.OwnSide, Is.EqualTo(Side.Alpha));
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
            Assert.That(status.SideScores, Has.Count.EqualTo(2));
            Assert.That(status.SideScores[0].Side, Is.EqualTo(Side.Alpha));
            Assert.That(status.SideScores[0].Score, Is.EqualTo(1));
            Assert.That(status.SideScores[1].Score, Is.Zero);
            Assert.That(status.Detail, Is.SameAs(_status.Execution));

            var execution = _status.Execution;
            Assert.That(execution.Session, Is.Null, "there is no offline session on the client");
            Assert.That(execution.LocalPlayerId, Is.EqualTo(_self));
            Assert.That(execution.InteractTargetId, Is.Null);
            var view = execution.View;
            Assert.That(view.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(view.RoundNumber, Is.EqualTo(2));
            Assert.That(view.WinsToWin, Is.EqualTo(3));
            Assert.That(view.RoundRemaining, Is.EqualTo(75f));
            Assert.That(view.PreparationRemaining, Is.Zero);
            Assert.That(view.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(view.GetRoundWins(Side.Bravo), Is.Zero);
            Assert.That(view.HasPlayer(_enemy), Is.True);
            Assert.That(view.HasPlayer(99), Is.False);
            Assert.That(view.GetSide(_ally), Is.EqualTo(Side.Alpha));
            Assert.That(view.GetSide(_enemy), Is.EqualTo(Side.Bravo));
            Assert.That(view.CountRemaining(Side.Alpha), Is.EqualTo(2));
            Assert.That(view.CountRemaining(Side.Bravo), Is.EqualTo(1), "the downed enemy remains, the fallen one does not");
            Assert.That(view.CountDowned(Side.Bravo), Is.EqualTo(1));
            Assert.That(view.CountDowned(Side.Alpha), Is.Zero);
            Assert.That(view.IsDowned(_enemy), Is.True);
            Assert.That(view.IsDowned(_self), Is.False);
            Assert.That(view.GetBleedoutRemaining(_enemy), Is.EqualTo(12f));
            Assert.That(view.GetBleedoutRemaining(_self), Is.Null);
            Assert.That(HudFormat.RevivePercent(view.GetReviveProgress(_enemy)), Is.EqualTo(40), "the percent the server sent comes back to the HUD");
            Assert.That(view.GetExecuteProgress(_enemy), Is.Zero);
            Assert.That(() => view.GetSide(99), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => view.GetRoundWins(new Side(2)), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(29, 29)]
        [TestCase(57, 57)]
        [TestCase(99, 99)]
        [TestCase(100, 100)]
        public void TheRevivePercentReadsBackUnchanged(int sent, int shown)
        {
            var snapshot = Snapshot(phase: RoundPhase.Live);
            Row(snapshot, _self).Downed = new DownedSync(true, 5, (byte)sent);

            _status.Apply(snapshot);

            Assert.That(HudFormat.RevivePercent(_status.Execution.View.GetReviveProgress(_self)), Is.EqualTo(shown));
        }

        [Test]
        public void TheEnemyTheLocalPlayerIsExecutingIsTheInteractTarget()
        {
            var snapshot = Snapshot(phase: RoundPhase.Live);
            Row(snapshot, _enemy).Downed = new DownedSync(true, 12, 0).WithExecutor(_self, 45);
            Row(snapshot, _otherEnemy).Downed = new DownedSync(true, 8, 0);

            _status.Apply(snapshot);

            var execution = _status.Execution;
            Assert.That(execution.InteractTargetId, Is.EqualTo(_enemy));
            Assert.That(HudFormat.RevivePercent(execution.View.GetExecuteProgress(_enemy)), Is.EqualTo(45), "the percent the server sent comes back to the HUD");
            Assert.That(execution.View.GetExecuteProgress(_otherEnemy), Is.Zero);
            Assert.That(execution.View.GetExecuteProgress(99), Is.Zero);
        }

        [Test]
        public void AnExecutionBySomeoneElseIsNotTheLocalPlayersTarget()
        {
            var snapshot = Snapshot(phase: RoundPhase.Live);
            Row(snapshot, _enemy).Downed = new DownedSync(true, 12, 0).WithExecutor(_ally, 30);

            _status.Apply(snapshot);

            Assert.That(_status.Execution.InteractTargetId, Is.Null);
            Assert.That(HudFormat.RevivePercent(_status.Execution.View.GetExecuteProgress(_enemy)), Is.EqualTo(30));
        }

        [Test]
        public void WithoutTheLocalPlayerThereIsNoInteractTarget()
        {
            var snapshot = Snapshot(phase: RoundPhase.Live, includeSelf: false);
            Row(snapshot, _enemy).Downed = new DownedSync(true, 12, 0).WithExecutor(_self, 30);

            _status.Apply(snapshot);

            Assert.That(_status.Execution.LocalPlayerId, Is.Null);
            Assert.That(_status.Execution.InteractTargetId, Is.Null);
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(66, 66)]
        [TestCase(99, 99)]
        [TestCase(100, 100)]
        public void TheExecutePercentReadsBackUnchanged(int sent, int shown)
        {
            var snapshot = Snapshot(phase: RoundPhase.Live);
            Row(snapshot, _enemy).Downed = new DownedSync(true, 5, 0).WithExecutor(_self, (byte)sent);

            _status.Apply(snapshot);

            Assert.That(HudFormat.RevivePercent(_status.Execution.View.GetExecuteProgress(_enemy)), Is.EqualTo(shown));
        }

        [Test]
        public void AdvancingTheExecutionReplacesTheStatus()
        {
            var first = Snapshot(phase: RoundPhase.Live, remaining: 60);
            Row(first, _enemy).Downed = new DownedSync(true, 12, 0).WithExecutor(_self, 20);
            _status.Apply(first);
            var before = _status.Execution;
            var next = Snapshot(phase: RoundPhase.Live, remaining: 60);
            Row(next, _enemy).Downed = new DownedSync(true, 12, 0).WithExecutor(_self, 70);

            Assert.That(_status.Apply(next), Is.True);
            Assert.That(HudFormat.RevivePercent(_status.Execution.View.GetExecuteProgress(_enemy)), Is.EqualTo(70));
            Assert.That(HudFormat.RevivePercent(before.View.GetExecuteProgress(_enemy)), Is.EqualTo(20), "the status already handed out keeps its own view");
        }

        [Test]
        public void AStoppedExecutionClearsTheInteractTarget()
        {
            var first = Snapshot(phase: RoundPhase.Live, remaining: 60);
            Row(first, _enemy).Downed = new DownedSync(true, 12, 0).WithExecutor(_self, 50);
            _status.Apply(first);
            var next = Snapshot(phase: RoundPhase.Live, remaining: 60);
            Row(next, _enemy).Downed = new DownedSync(true, 12, 0);

            Assert.That(_status.Apply(next), Is.True);
            Assert.That(_status.Execution.InteractTargetId, Is.Null);
            Assert.That(_status.Execution.View.GetExecuteProgress(_enemy), Is.Zero);
        }

        [Test]
        public void DuringPreparationThePreviousRoundAndTheCountdownAreShown()
        {
            var snapshot = Snapshot(phase: RoundPhase.Preparing, roundNumber: 2, roundsToWin: 3, alphaWins: 0, bravoWins: 1, remaining: 3);
            ref var match = ref snapshot.Match;
            match.HasLastRound = true;
            match.LastRoundEnd = RoundEndReason.AttackersEliminated;
            match.HasLastRoundWinner = true;
            match.LastRoundWinner = Team.Bravo;

            _status.Apply(snapshot);

            var view = _status.Execution.View;
            Assert.That(view.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(view.PreparationRemaining, Is.EqualTo(3f));
            Assert.That(view.RoundRemaining, Is.Zero);
            Assert.That(view.HasFinishedRound, Is.True);
            Assert.That(view.LastRoundWinner, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void ADrawnPreviousRoundHasNoWinner()
        {
            var snapshot = Snapshot(phase: RoundPhase.Preparing, roundNumber: 2);
            ref var match = ref snapshot.Match;
            match.HasLastRound = true;
            match.LastRoundEnd = RoundEndReason.TimeUp;

            _status.Apply(snapshot);

            Assert.That(_status.Execution.View.HasFinishedRound, Is.True);
            Assert.That(_status.Execution.View.LastRoundWinner, Is.Null);
        }

        [TestCase(Team.Alpha)]
        [TestCase(Team.Bravo)]
        public void TheMatchWinnerIsShownWhenTheMatchEnds(Team winner)
        {
            var snapshot = Snapshot(phase: RoundPhase.Ended, roundNumber: 3, roundsToWin: 2, alphaWins: 2, bravoWins: 1, remaining: 0);
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Ended;
            match.HasWinner = true;
            match.Winner = winner;

            _status.Apply(snapshot);

            Assert.That(_status.Status.IsEnded, Is.True);
            Assert.That(_status.Status.Winner, Is.EqualTo(Side.FromTeam(winner)));
            Assert.That(_status.Execution.View.Winner, Is.EqualTo(Side.FromTeam(winner)));
        }

        [Test]
        public void AWinnerBeforeTheEndIsNotShown()
        {
            var snapshot = Snapshot(phase: RoundPhase.Live);
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Bravo;

            _status.Apply(snapshot);

            Assert.That(_status.Execution.View.Winner, Is.Null);
            Assert.That(_status.Status.Winner, Is.Null);
        }

        [Test]
        public void TheSameValuesDoNotReplaceTheStatus()
        {
            _status.Apply(Snapshot(phase: RoundPhase.Live, remaining: 60));
            var first = _status.Status;

            var changed = _status.Apply(Snapshot(phase: RoundPhase.Live, remaining: 60));

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(first));
        }

        [Test]
        public void AChangedRowReplacesTheStatus()
        {
            _status.Apply(Snapshot(phase: RoundPhase.Live, remaining: 60));
            var first = _status.Status;
            var snapshot = Snapshot(phase: RoundPhase.Live, remaining: 60);
            Row(snapshot, _enemy).Downed = new DownedSync(true, 15, 0);

            var changed = _status.Apply(snapshot);

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(first));
            Assert.That(_status.Execution.View.IsDowned(_enemy), Is.True);
            Assert.That(first.Detail, Is.Not.SameAs(_status.Status.Detail), "the status already handed out keeps its own view");
        }

        [Test]
        public void ARemainingTimeChangeReplacesTheStatus()
        {
            _status.Apply(Snapshot(phase: RoundPhase.Live, remaining: 60));

            Assert.That(_status.Apply(Snapshot(phase: RoundPhase.Live, remaining: 59)), Is.True);
            Assert.That(_status.Status.RemainingSeconds, Is.EqualTo(59f));
        }

        [Test]
        public void WithoutTheLocalPlayerThereIsNoOwnSide()
        {
            var snapshot = Snapshot(phase: RoundPhase.Live, includeSelf: false);

            _status.Apply(snapshot);

            Assert.That(_status.Status.OwnSide, Is.Null);
            Assert.That(_status.Execution.LocalPlayerId, Is.Null);
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(phase: RoundPhase.Live));
            var other = Snapshot(phase: RoundPhase.Live);
            other.Match.Objective = MatchObjective.Elimination;

            Assert.That(_status.Apply(other), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Execution, Is.Null);
            Assert.That(_status.Apply(other), Is.False);
            Assert.That(_status.Clear(), Is.False);
        }

        [Test]
        public void ANullSnapshotIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        private static SnapshotMessage Snapshot(
            RoundPhase phase,
            int roundNumber = 1,
            int roundsToWin = 3,
            int alphaWins = 0,
            int bravoWins = 0,
            int remaining = 120,
            bool includeSelf = true)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = GameModeState.Playing;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.Execution;
            match.HasRound = true;
            match.Round = phase;
            match.RoundNumber = roundNumber;
            match.RoundsToWin = roundsToWin;
            match.AlphaPoints = alphaWins;
            match.BravoPoints = bravoWins;
            match.AlphaScore = alphaWins;
            match.BravoScore = bravoWins;

            if (includeSelf)
            {
                AddRow(snapshot, _self, Team.Alpha);
            }

            AddRow(snapshot, _enemy, Team.Bravo);
            AddRow(snapshot, _ally, Team.Alpha);
            AddRow(snapshot, _otherEnemy, Team.Bravo);
            return snapshot;
        }

        private static void AddRow(SnapshotMessage snapshot, int playerId, Team team)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = 100;
            row.WeaponId = "";
        }

        private static ref PlayerSnapshot Row(SnapshotMessage snapshot, int playerId)
        {
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId == playerId)
                {
                    return ref players[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no row for this player");
        }
    }
}
