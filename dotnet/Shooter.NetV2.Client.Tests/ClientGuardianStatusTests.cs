using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientGuardianStatusTests
    {
        private const int _self = 1;
        private const int _enemy = 2;
        private const int _ally = 5;

        private static readonly Side[] _bothSides = { Side.Alpha, Side.Bravo };
        private static readonly Side[] _alphaOnly = { Side.Alpha };

        [Test]
        public void AGuardianSnapshotBecomesAModeStatusShapedLikeTheOfflineOne()
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 3, bravoScore: 1, remaining: 42);
            AddLeader(snapshot, Team.Alpha, _self, fallen: false);
            AddLeader(snapshot, Team.Bravo, _enemy, fallen: false);

            Assert.That(status.Apply(snapshot), Is.True);

            var shown = status.Status;
            Assert.That(shown, Is.Not.Null);
            Assert.That(shown.RemainingSeconds, Is.EqualTo(42f));
            Assert.That(shown.SideScores, Is.EqualTo(new[] { new SideScore(Side.Alpha, 3d), new SideScore(Side.Bravo, 1d) }));
            Assert.That(shown.OwnSide, Is.EqualTo(Side.Alpha));
            Assert.That(shown.RespawnSeconds, Is.Null);
            Assert.That(shown.IsEnded, Is.False);
            Assert.That(shown.Winner, Is.Null);
            var detail = (GuardianStatus)shown.Detail;
            Assert.That(status.Guardian, Is.SameAs(detail));
            Assert.That(detail.LocalPlayerId, Is.EqualTo(_self));
            Assert.That(detail.IsLocalPlayerEliminated, Is.False);
            Assert.That(detail.Leaders.LeaderSides, Is.EqualTo(_bothSides));
            Assert.That(detail.Leaders.GetLeader(Side.Alpha), Is.EqualTo(_self));
            Assert.That(detail.Leaders.GetLeader(Side.Bravo), Is.EqualTo(_enemy));
            Assert.That(detail.Leaders.HasLeaderFallen(Side.Alpha), Is.False);
            Assert.That(detail.Leaders.HasLeaderFallen(Side.Bravo), Is.False);
            Assert.That(status.LeaderOf(Team.Alpha), Is.EqualTo(_self));
            Assert.That(status.LeaderOf(Team.Bravo), Is.EqualTo(_enemy));
            Assert.That(status.IsStandingLeader(_self), Is.True);
            Assert.That(status.IsStandingLeader(_enemy), Is.True);
            Assert.That(status.IsStandingLeader(_ally), Is.False);
        }

        [Test]
        public void ADroppedLeaderHasFallen()
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 0, bravoScore: 1, remaining: 30);
            AddLeader(snapshot, Team.Alpha, _self, fallen: false);
            AddLeader(snapshot, Team.Bravo, _enemy, fallen: true);

            status.Apply(snapshot);

            Assert.That(status.HasLeaderFallen(Team.Bravo), Is.True);
            Assert.That(status.HasLeaderFallen(Team.Alpha), Is.False);
            Assert.That(status.IsStandingLeader(_enemy), Is.False, "a fallen leader loses the marker");
            Assert.That(status.LeaderOf(Team.Bravo), Is.EqualTo(_enemy));
            Assert.That(status.Guardian.Leaders.HasLeaderFallen(Side.Bravo), Is.True);
            Assert.That(status.Guardian.Leaders.HasLeaderFallen(Side.Alpha), Is.False);
        }

        [Test]
        public void ADeadPlayerWhoseLeaderFellIsEliminated()
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 0, bravoScore: 2, remaining: 30, selfHealth: 0);
            AddPlayer(snapshot, _ally, Team.Alpha, 0);
            AddLeader(snapshot, Team.Alpha, _ally, fallen: true);
            AddLeader(snapshot, Team.Bravo, _enemy, fallen: false);

            status.Apply(snapshot);

            Assert.That(status.Guardian.IsLocalPlayerEliminated, Is.True);
            Assert.That(status.IsStandingLeader(_self), Is.False);
        }

        [TestCase(100, true, GameModeState.Playing)]
        [TestCase(0, false, GameModeState.Playing)]
        [TestCase(0, true, GameModeState.Ended)]
        public void APlayerIsNotEliminatedWhileStandingOrWhileTheirLeaderStandsOrAfterTheMatch(int health, bool leaderFallen, GameModeState state)
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30, selfHealth: health);
            snapshot.Match.State = state;
            AddPlayer(snapshot, _ally, Team.Alpha, 100);
            AddLeader(snapshot, Team.Alpha, _ally, leaderFallen);
            AddLeader(snapshot, Team.Bravo, _enemy, fallen: false);

            status.Apply(snapshot);

            Assert.That(status.Guardian.IsLocalPlayerEliminated, Is.False);
        }

        [Test]
        public void TheSameStateDoesNotMakeANewStatus()
        {
            var status = new ClientGuardianStatus(_self);
            var first = Snapshot(10, alphaScore: 1, bravoScore: 0, remaining: 30);
            AddLeader(first, Team.Alpha, _self, fallen: false);
            AddLeader(first, Team.Bravo, _enemy, fallen: false);
            Assert.That(status.Apply(first), Is.True);
            var shown = status.Status;

            var second = Snapshot(11, alphaScore: 1, bravoScore: 0, remaining: 30);
            AddLeader(second, Team.Alpha, _self, fallen: false);
            AddLeader(second, Team.Bravo, _enemy, fallen: false);

            Assert.That(status.Apply(second), Is.False);
            Assert.That(status.Status, Is.SameAs(shown));
        }

        [Test]
        public void ALeaderFallingMakesANewStatusWithNewLeaders()
        {
            var status = new ClientGuardianStatus(_self);
            var first = Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30);
            AddLeader(first, Team.Alpha, _self, fallen: false);
            AddLeader(first, Team.Bravo, _enemy, fallen: false);
            status.Apply(first);
            var leaders = status.Guardian.Leaders;

            var second = Snapshot(11, alphaScore: 1, bravoScore: 0, remaining: 30);
            AddLeader(second, Team.Alpha, _self, fallen: false);
            AddLeader(second, Team.Bravo, _enemy, fallen: true);

            Assert.That(status.Apply(second), Is.True);
            Assert.That(status.Guardian.Leaders, Is.Not.SameAs(leaders), "the HUD may still hold the old status");
            Assert.That(leaders.HasLeaderFallen(Side.Bravo), Is.False);
            Assert.That(status.Guardian.Leaders.HasLeaderFallen(Side.Bravo), Is.True);
        }

        [Test]
        public void AScoreChangeKeepsTheLeaders()
        {
            var status = new ClientGuardianStatus(_self);
            var first = Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30);
            AddLeader(first, Team.Alpha, _self, fallen: false);
            AddLeader(first, Team.Bravo, _enemy, fallen: false);
            status.Apply(first);
            var leaders = status.Guardian.Leaders;

            var second = Snapshot(11, alphaScore: 1, bravoScore: 0, remaining: 29);
            AddLeader(second, Team.Alpha, _self, fallen: false);
            AddLeader(second, Team.Bravo, _enemy, fallen: false);

            Assert.That(status.Apply(second), Is.True);
            Assert.That(status.Guardian.Leaders, Is.SameAs(leaders));
            Assert.That(status.Status.SideScores[0].Score, Is.EqualTo(1d));
        }

        [Test]
        public void BeforeTheLeadersAreChosenTheStatusHasNoLeaders()
        {
            var status = new ClientGuardianStatus(_self);

            Assert.That(status.Apply(Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30)), Is.True);

            Assert.That(status.Guardian.Leaders.LeaderSides, Is.Empty);
            Assert.That(status.LeaderOf(Team.Alpha), Is.Null);
            Assert.That(status.IsStandingLeader(_self), Is.False);
        }

        [Test]
        public void BrokenLeaderRowsAreSkipped()
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30);
            AddLeader(snapshot, Team.Alpha, _self, fallen: false);
            AddLeader(snapshot, Team.Alpha, _ally, fallen: true);
            AddLeader(snapshot, Team.Bravo, _self, fallen: false);
            ref var ownerless = ref snapshot.ObjectiveItems.Add();
            ownerless.ItemId = 9;
            ownerless.Kind = ObjectiveItemKind.Leader;
            ownerless.Team = Team.Bravo;
            ownerless.State = ObjectiveItemState.Dropped;

            Assert.That(status.Apply(snapshot), Is.True);

            Assert.That(status.LeaderOf(Team.Alpha), Is.EqualTo(_self), "the first row of a team counts");
            Assert.That(status.HasLeaderFallen(Team.Alpha), Is.False);
            Assert.That(status.LeaderOf(Team.Bravo), Is.Null, "one player cannot lead both teams");
            Assert.That(status.Guardian.Leaders.LeaderSides, Is.EqualTo(_alphaOnly));
        }

        [Test]
        public void OtherItemsAreNotLeaders()
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30);
            ref var flag = ref snapshot.ObjectiveItems.Add();
            flag.ItemId = 0;
            flag.Kind = ObjectiveItemKind.Flag;
            flag.Team = Team.Alpha;
            flag.HasOwner = true;
            flag.OwnerId = _self;
            flag.State = ObjectiveItemState.Carried;
            flag.HasCarrier = true;
            flag.CarrierId = _self;

            status.Apply(snapshot);

            Assert.That(status.LeaderOf(Team.Alpha), Is.Null);
        }

        [Test]
        public void AnEndedMatchShowsTheWinner()
        {
            var status = new ClientGuardianStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 2, bravoScore: 5, remaining: 0);
            AddLeader(snapshot, Team.Alpha, _self, fallen: false);
            AddLeader(snapshot, Team.Bravo, _enemy, fallen: false);
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Alpha;

            status.Apply(snapshot);

            Assert.That(status.Status.IsEnded, Is.True);
            Assert.That(status.Status.Winner, Is.EqualTo(Side.Alpha), "the server decides the winner, not the kills shown here");
        }

        [Test]
        public void AnotherModeClearsTheStatus()
        {
            var status = new ClientGuardianStatus(_self);
            var guardian = Snapshot(10, alphaScore: 0, bravoScore: 0, remaining: 30);
            AddLeader(guardian, Team.Alpha, _self, fallen: false);
            status.Apply(guardian);

            Assert.That(status.Apply(Snapshot(11, alphaScore: 0, bravoScore: 0, remaining: 30, objective: MatchObjective.None)), Is.True);
            Assert.That(status.Status, Is.Null);
            Assert.That(status.Guardian, Is.Null);
            Assert.That(status.LeaderOf(Team.Alpha), Is.Null);
            Assert.That(status.IsStandingLeader(_self), Is.False);
            Assert.That(status.Apply(Snapshot(12, alphaScore: 0, bravoScore: 0, remaining: 30, objective: MatchObjective.None)), Is.False);
        }

        [Test]
        public void AnOlderSnapshotIsIgnoredUntilReset()
        {
            var status = new ClientGuardianStatus(_self);
            status.Apply(Snapshot(10, alphaScore: 2, bravoScore: 0, remaining: 30));

            Assert.That(status.Apply(Snapshot(9, alphaScore: 1, bravoScore: 0, remaining: 31)), Is.False);
            Assert.That(status.Status.SideScores[0].Score, Is.EqualTo(2d));

            status.Reset();
            Assert.That(status.Status, Is.Null);
            Assert.That(status.Apply(Snapshot(1, alphaScore: 0, bravoScore: 0, remaining: 300)), Is.True, "a new connection starts its ticks again");
        }

        [Test]
        public void ApplyRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientGuardianStatus(_self).Apply(null));
        }

        private static SnapshotMessage Snapshot(
            int tick,
            int alphaScore,
            int bravoScore,
            int remaining,
            int selfHealth = 100,
            MatchObjective objective = MatchObjective.Guardian)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = objective;
            snapshot.Match.AlphaScore = alphaScore;
            snapshot.Match.BravoScore = bravoScore;
            snapshot.Match.RemainingSeconds = remaining;
            AddPlayer(snapshot, _self, Team.Alpha, selfHealth);
            AddPlayer(snapshot, _enemy, Team.Bravo, 100);
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team, int health)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = health;
        }

        private static void AddLeader(SnapshotMessage snapshot, Team team, int leader, bool fallen)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = Side.FromTeam(team).Id;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Leader;
            item.Team = team;
            item.HasOwner = true;
            item.OwnerId = leader;
            item.Position = Vector3.Zero;
            item.State = fallen ? ObjectiveItemState.Dropped : ObjectiveItemState.Carried;
            item.HasCarrier = !fallen;
            item.CarrierId = fallen ? 0 : leader;
        }
    }
}
