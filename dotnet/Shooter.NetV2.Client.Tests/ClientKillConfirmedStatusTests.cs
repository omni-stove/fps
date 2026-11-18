using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientKillConfirmedStatusTests
    {
        private const int _self = 1;
        private const int _enemy = 2;

        [Test]
        public void AKillConfirmedSnapshotBecomesAModeStatusShapedLikeTheOfflineOne()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 3, bravoScore: 1, alphaConfirmed: 3, bravoConfirmed: 1, remaining: 42);
            AddTag(snapshot, 5, _enemy, Team.Bravo, new Vector3(1f, 0f, 2f));

            Assert.That(status.Apply(snapshot), Is.True);

            var shown = status.Status;
            Assert.That(shown, Is.Not.Null);
            Assert.That(shown.RemainingSeconds, Is.EqualTo(42f));
            Assert.That(shown.SideScores, Is.EqualTo(new[] { new SideScore(Side.Alpha, 3d), new SideScore(Side.Bravo, 1d) }));
            Assert.That(shown.OwnSide, Is.EqualTo(Side.Alpha));
            Assert.That(shown.RespawnSeconds, Is.Null);
            Assert.That(shown.IsEnded, Is.False);
            Assert.That(shown.Winner, Is.Null);
            Assert.That(shown.Detail, Is.SameAs(status.Detail));
            var detail = (KillConfirmed)shown.Detail;
            Assert.That(detail.GetConfirmed(Side.Alpha), Is.EqualTo(3));
            Assert.That(detail.GetConfirmed(Side.Bravo), Is.EqualTo(1));
            Assert.That(detail.GetDenied(Side.Alpha), Is.Zero, "the snapshot has no denied counts");
            Assert.That(detail.Tags.Count, Is.EqualTo(1));
            Assert.That(detail.Tags[0].Id, Is.EqualTo(5));
            Assert.That(detail.Tags[0].OwnerId, Is.EqualTo(_enemy));
            Assert.That(detail.Tags[0].OwnerSide, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void TheSameStateDoesNotMakeANewStatus()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var first = Snapshot(10, alphaScore: 1, bravoScore: 0, alphaConfirmed: 1, bravoConfirmed: 0, remaining: 30);
            AddTag(first, 1, _enemy, Team.Bravo, Vector3.Zero);
            Assert.That(status.Apply(first), Is.True);
            var shown = status.Status;

            var second = Snapshot(11, alphaScore: 1, bravoScore: 0, alphaConfirmed: 1, bravoConfirmed: 0, remaining: 30);
            AddTag(second, 1, _enemy, Team.Bravo, Vector3.Zero);

            Assert.That(status.Apply(second), Is.False);
            Assert.That(status.Status, Is.SameAs(shown));
        }

        [Test]
        public void TagsThatLeaveTheSnapshotLeaveTheDetail()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var first = Snapshot(10, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30);
            AddTag(first, 1, _enemy, Team.Bravo, Vector3.Zero);
            AddTag(first, 2, _self, Team.Alpha, Vector3.One);
            status.Apply(first);
            Assert.That(status.Detail.Tags.Count, Is.EqualTo(2));

            var second = Snapshot(11, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30);
            AddTag(second, 2, _self, Team.Alpha, Vector3.One);

            Assert.That(status.Apply(second), Is.True, "a tag going away is a change even when the counts stay");
            Assert.That(status.Detail.Tags.Count, Is.EqualTo(1));
            Assert.That(status.Detail.FindTag(1), Is.Null);
            Assert.That(status.Detail.FindTag(2), Is.Not.Null);
        }

        [Test]
        public void AReusedTagIdWithAnotherOwnerReplacesTheTag()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var first = Snapshot(10, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30);
            AddTag(first, 1, _enemy, Team.Bravo, Vector3.Zero);
            status.Apply(first);

            var second = Snapshot(11, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30);
            AddTag(second, 1, _self, Team.Alpha, Vector3.Zero);

            Assert.That(status.Apply(second), Is.True);
            Assert.That(status.Detail.Tags.Count, Is.EqualTo(1));
            Assert.That(status.Detail.Tags[0].OwnerId, Is.EqualTo(_self));
            Assert.That(status.Detail.Tags[0].OwnerSide, Is.EqualTo(Side.Alpha));
        }

        [Test]
        public void AnEndedMatchShowsTheWinner()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 2, bravoScore: 5, alphaConfirmed: 2, bravoConfirmed: 5, remaining: 0);
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Bravo;

            status.Apply(snapshot);

            Assert.That(status.Status.IsEnded, Is.True);
            Assert.That(status.Status.Winner, Is.EqualTo(Side.Bravo));
            Assert.That(status.Status.RemainingSeconds, Is.Zero);
        }

        [Test]
        public void AnEndedDrawHasNoWinner()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 1, bravoScore: 1, alphaConfirmed: 1, bravoConfirmed: 1, remaining: 0);
            snapshot.Match.State = GameModeState.Ended;

            status.Apply(snapshot);

            Assert.That(status.Status.IsEnded, Is.True);
            Assert.That(status.Status.Winner, Is.Null);
        }

        [Test]
        public void AnOlderSnapshotIsIgnored()
        {
            var status = new ClientKillConfirmedStatus(_self);
            status.Apply(Snapshot(10, alphaScore: 2, bravoScore: 0, alphaConfirmed: 2, bravoConfirmed: 0, remaining: 30));

            Assert.That(status.Apply(Snapshot(9, alphaScore: 1, bravoScore: 0, alphaConfirmed: 1, bravoConfirmed: 0, remaining: 31)), Is.False);
            Assert.That(status.Status.SideScores[0].Score, Is.EqualTo(2d));
        }

        [Test]
        public void AnotherModeClearsTheStatus()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var first = Snapshot(10, alphaScore: 1, bravoScore: 0, alphaConfirmed: 1, bravoConfirmed: 0, remaining: 30);
            AddTag(first, 1, _enemy, Team.Bravo, Vector3.Zero);
            status.Apply(first);

            var other = Snapshot(11, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30);
            other.Match.Objective = MatchObjective.None;

            Assert.That(status.Apply(other), Is.True);
            Assert.That(status.Status, Is.Null);
            Assert.That(status.Detail.Tags, Is.Empty);
            Assert.That(status.Detail.GetConfirmed(Side.Alpha), Is.Zero);
            Assert.That(status.Apply(Snapshot(12, 0, 0, 0, 0, 30, MatchObjective.None)), Is.False, "staying in another mode is no change");
        }

        [Test]
        public void ResetAcceptsTheTicksOfTheNextConnection()
        {
            var status = new ClientKillConfirmedStatus(_self);
            status.Apply(Snapshot(100, alphaScore: 1, bravoScore: 0, alphaConfirmed: 1, bravoConfirmed: 0, remaining: 30));

            status.Reset();

            Assert.That(status.Status, Is.Null);
            Assert.That(status.Apply(Snapshot(1, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 60)), Is.True);
            Assert.That(status.Status.RemainingSeconds, Is.EqualTo(60f));
        }

        [Test]
        public void WithoutTheLocalRowThereIsNoOwnSide()
        {
            var status = new ClientKillConfirmedStatus(99);

            status.Apply(Snapshot(10, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30));

            Assert.That(status.Status.OwnSide, Is.Null);
        }

        [Test]
        public void ItemsThatAreNotTagsAreNotCopied()
        {
            var status = new ClientKillConfirmedStatus(_self);
            var snapshot = Snapshot(10, alphaScore: 0, bravoScore: 0, alphaConfirmed: 0, bravoConfirmed: 0, remaining: 30);
            ref var flag = ref snapshot.ObjectiveItems.Add();
            flag.ItemId = 3;
            flag.Present = ObjectiveItemFields.All;
            flag.Kind = ObjectiveItemKind.Flag;
            flag.Team = Team.Alpha;
            flag.State = ObjectiveItemState.Home;

            status.Apply(snapshot);

            Assert.That(status.Detail.Tags, Is.Empty);
        }

        [Test]
        public void ApplyRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientKillConfirmedStatus(_self).Apply(null));
        }

        private static SnapshotMessage Snapshot(
            int tick,
            int alphaScore,
            int bravoScore,
            int alphaConfirmed,
            int bravoConfirmed,
            int remaining,
            MatchObjective objective = MatchObjective.KillConfirmed)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = objective;
            snapshot.Match.AlphaScore = alphaScore;
            snapshot.Match.BravoScore = bravoScore;
            snapshot.Match.AlphaPoints = alphaConfirmed;
            snapshot.Match.BravoPoints = bravoConfirmed;
            snapshot.Match.RemainingSeconds = remaining;
            AddPlayer(snapshot, _self, Team.Alpha);
            AddPlayer(snapshot, _enemy, Team.Bravo);
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = 100;
        }

        private static void AddTag(SnapshotMessage snapshot, int tagId, int ownerId, Team team, Vector3 position)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = tagId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Tag;
            item.Team = team;
            item.HasOwner = true;
            item.OwnerId = ownerId;
            item.Position = position;
            item.State = ObjectiveItemState.Dropped;
        }
    }
}
