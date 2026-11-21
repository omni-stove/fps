using System;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientInfectedStatusTests
    {
        private const int _self = 3;

        private ClientInfectedStatus _status;

        [SetUp]
        public void SetUp()
        {
            _status = new ClientInfectedStatus(_self);
        }

        [Test]
        public void TheCountdownBeforeTheOutbreakIsShownInTheSameShapeAsOffline()
        {
            var changed = _status.Apply(Snapshot(survivors: 3, infected: 0, secondsUntilOutbreak: 7, selfTeam: Team.Alpha));

            Assert.That(changed, Is.True);
            var status = _status.Status;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Detail, Is.InstanceOf<InfectedStatus>(), "the HUD formatter registered for InfectedStatus reads it");
            Assert.That(status.Detail, Is.SameAs(_status.Infected));
            var infected = _status.Infected;
            Assert.That(infected.HasBrokenOut, Is.False);
            Assert.That(infected.SecondsUntilOutbreak, Is.EqualTo(7f));
            Assert.That(infected.SurvivorCount, Is.EqualTo(3));
            Assert.That(infected.InfectedCount, Is.Zero);
            Assert.That(infected.IsLocalPlayerInfected, Is.False);
            Assert.That(status.RemainingSeconds, Is.EqualTo(300f));
            Assert.That(status.OwnSide, Is.EqualTo(Infected.SurvivorSide));
            Assert.That(status.RespawnSeconds, Is.Null);
            Assert.That(status.IsEnded, Is.False);
            Assert.That(status.Winner, Is.Null);
        }

        [Test]
        public void ZeroSecondsMarksTheOutbreakAndTheOwnRowTellsWhetherThePlayerIsInfected()
        {
            _status.Apply(Snapshot(survivors: 2, infected: 1, secondsUntilOutbreak: 0, selfTeam: Team.Bravo, survivorScore: 0, infectedScore: 2));

            var infected = _status.Infected;
            Assert.That(infected.HasBrokenOut, Is.True);
            Assert.That(infected.SecondsUntilOutbreak, Is.Zero);
            Assert.That(infected.SurvivorCount, Is.EqualTo(2));
            Assert.That(infected.InfectedCount, Is.EqualTo(1));
            Assert.That(infected.IsLocalPlayerInfected, Is.True);
            var status = _status.Status;
            Assert.That(status.OwnSide, Is.EqualTo(Infected.InfectedSide));
            Assert.That(status.SideScores, Has.Count.EqualTo(2));
            Assert.That(status.SideScores[0].Side, Is.EqualTo(Infected.SurvivorSide));
            Assert.That(status.SideScores[0].Score, Is.Zero);
            Assert.That(status.SideScores[1].Side, Is.EqualTo(Infected.InfectedSide));
            Assert.That(status.SideScores[1].Score, Is.EqualTo(2d));
        }

        [Test]
        public void WithoutAnOwnRowTheLocalSideIsUnknown()
        {
            _status.Apply(Snapshot(survivors: 1, infected: 1, secondsUntilOutbreak: 0, selfTeam: null));

            Assert.That(_status.Infected.IsLocalPlayerInfected, Is.Null);
            Assert.That(_status.Status.OwnSide, Is.Null);
        }

        [Test]
        public void TheSameValuesKeepTheShownStatus()
        {
            _status.Apply(Snapshot(survivors: 2, infected: 1, secondsUntilOutbreak: 0, selfTeam: Team.Alpha));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(survivors: 2, infected: 1, secondsUntilOutbreak: 0, selfTeam: Team.Alpha));

            Assert.That(changed, Is.False);
            Assert.That(_status.Status, Is.SameAs(shown));
        }

        [Test]
        public void TurningChangesTheStatus()
        {
            _status.Apply(Snapshot(survivors: 2, infected: 1, secondsUntilOutbreak: 0, selfTeam: Team.Alpha));
            var shown = _status.Status;

            var changed = _status.Apply(Snapshot(survivors: 1, infected: 2, secondsUntilOutbreak: 0, selfTeam: Team.Bravo, infectedScore: 1));

            Assert.That(changed, Is.True);
            Assert.That(_status.Status, Is.Not.SameAs(shown));
            Assert.That(_status.Infected.IsLocalPlayerInfected, Is.True);
            Assert.That(_status.Infected.SurvivorCount, Is.EqualTo(1));
            Assert.That(((InfectedStatus)shown.Detail).SurvivorCount, Is.EqualTo(2), "the old status is not changed");
        }

        [TestCase(Team.Alpha)]
        [TestCase(Team.Bravo)]
        public void TheEndShowsTheWinningSide(Team winner)
        {
            _status.Apply(Snapshot(survivors: 1, infected: 2, secondsUntilOutbreak: 0, selfTeam: Team.Alpha, state: GameModeState.Ended, winner: winner, remaining: 0));

            var status = _status.Status;
            Assert.That(status.IsEnded, Is.True);
            Assert.That(status.Winner, Is.EqualTo(Side.FromTeam(winner)));
            Assert.That(status.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void AnotherObjectiveClearsTheStatus()
        {
            _status.Apply(Snapshot(survivors: 2, infected: 1, secondsUntilOutbreak: 0, selfTeam: Team.Alpha));
            var other = Snapshot(survivors: 2, infected: 1, secondsUntilOutbreak: 0, selfTeam: Team.Alpha);
            other.Match.Objective = MatchObjective.Hardpoint;

            Assert.That(_status.Apply(other), Is.True);
            Assert.That(_status.Status, Is.Null);
            Assert.That(_status.Infected, Is.Null);
            Assert.That(_status.Clear(), Is.False);
        }

        [Test]
        public void ANullSnapshotIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => _status.Apply(null));
        }

        private static SnapshotMessage Snapshot(
            int survivors,
            int infected,
            int secondsUntilOutbreak,
            Team? selfTeam,
            int survivorScore = 0,
            int infectedScore = 0,
            int remaining = 300,
            GameModeState state = GameModeState.Playing,
            Team? winner = null)
        {
            var snapshot = new SnapshotMessage { Tick = 10 };
            ref var match = ref snapshot.Match;
            match.State = state;
            match.HasWinner = winner.HasValue;
            match.Winner = winner ?? Team.Alpha;
            match.RemainingSeconds = remaining;
            match.Objective = MatchObjective.Infected;
            match.AlphaPoints = survivors;
            match.BravoPoints = infected;
            match.AlphaScore = survivorScore;
            match.BravoScore = infectedScore;
            match.ObjectiveRemainingSeconds = secondsUntilOutbreak;

            if (selfTeam.HasValue)
            {
                ref var self = ref snapshot.Players.Add();
                self.PlayerId = _self;
                self.Team = selfTeam.Value;
                self.WeaponId = "";
            }

            ref var other = ref snapshot.Players.Add();
            other.PlayerId = _self + 1;
            other.Team = Team.Bravo;
            other.WeaponId = "";
            return snapshot;
        }
    }
}
