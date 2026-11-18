using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientCaptureTheFlagStatusTests
    {
        private const int _local = 1;
        private const int _enemy = 2;

        [Test]
        public void AMatchWithoutFlagsPublishesNothing()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            var snapshot = Snapshot();
            snapshot.Match.Objective = MatchObjective.Domination;

            Assert.That(status.Apply(snapshot), Is.False);
            Assert.That(status.Status, Is.Null);
        }

        [Test]
        public void TheStatusHasTheSameShapeAsOffline()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            var snapshot = Snapshot(remaining: 120, alphaPoints: 1, bravoPoints: 0);

            Assert.That(status.Apply(snapshot), Is.True);

            var mode = status.Status;
            Assert.That(mode.RemainingSeconds, Is.EqualTo(120f));
            Assert.That(mode.SideScores, Has.Count.EqualTo(2));
            Assert.That(mode.SideScores[0], Is.EqualTo(new SideScore(Side.Alpha, 1d)));
            Assert.That(mode.SideScores[1], Is.EqualTo(new SideScore(Side.Bravo, 0d)));
            Assert.That(mode.OwnSide, Is.EqualTo(Side.Alpha));
            Assert.That(mode.RespawnSeconds, Is.Null);
            Assert.That(mode.IsEnded, Is.False);
            Assert.That(mode.Winner, Is.Null);
            var flags = (CaptureTheFlagStatus)mode.Detail;
            Assert.That(flags.LocalPlayerId, Is.EqualTo(_local));
            Assert.That(flags.LocalSide, Is.EqualTo(Side.Alpha));
            Assert.That(flags.GetState(Side.Alpha), Is.EqualTo(FlagState.AtBase));
            Assert.That(flags.GetState(Side.Bravo), Is.EqualTo(FlagState.AtBase));
            Assert.That(flags.IsLocalCarrying, Is.False);
        }

        [Test]
        public void AnUnchangedSnapshotKeepsTheSameStatus()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            status.Apply(Snapshot());
            var first = status.Status;

            Assert.That(status.Apply(Snapshot()), Is.False);
            Assert.That(status.Status, Is.SameAs(first));

            Assert.That(status.Apply(Snapshot(remaining: 119)), Is.True, "the clock moved");
            Assert.That(status.Status, Is.Not.SameAs(first));
        }

        [Test]
        public void TheLocalCarrierSeesTheFlagTheyCarry()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            var snapshot = Snapshot(bravoFlag: (ObjectiveItemState.Carried, _local));

            status.Apply(snapshot);

            var flags = (CaptureTheFlagStatus)status.Status.Detail;
            Assert.That(flags.GetState(Side.Bravo), Is.EqualTo(FlagState.Carried));
            Assert.That(flags.GetCarrierId(Side.Bravo), Is.EqualTo(_local));
            Assert.That(flags.CarriedFlag, Is.EqualTo(Side.Bravo));
            Assert.That(flags.IsLocalCarrying, Is.True);
        }

        [Test]
        public void ADroppedFlagCountsDownWithTheObjectiveSeconds()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            status.Apply(Snapshot(remaining: 100));

            status.Apply(Snapshot(remaining: 100, objectiveSeconds: 20, alphaFlag: (ObjectiveItemState.Dropped, null)));

            var flags = (CaptureTheFlagStatus)status.Status.Detail;
            Assert.That(flags.GetState(Side.Alpha), Is.EqualTo(FlagState.Dropped));
            Assert.That(flags.GetReturnSeconds(Side.Alpha), Is.EqualTo(20f));
            Assert.That(flags.GetReturnSeconds(Side.Bravo), Is.Null);

            Assert.That(status.Apply(Snapshot(remaining: 99, objectiveSeconds: 19, alphaFlag: (ObjectiveItemState.Dropped, null))), Is.True);
            Assert.That(((CaptureTheFlagStatus)status.Status.Detail).GetReturnSeconds(Side.Alpha), Is.EqualTo(19f));
        }

        [Test]
        public void TheLaterOfTwoDroppedFlagsReturnsLaterByTheGapBetweenTheDrops()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            status.Apply(Snapshot(remaining: 100));
            status.Apply(Snapshot(remaining: 100, objectiveSeconds: 20, alphaFlag: (ObjectiveItemState.Dropped, null)));

            status.Apply(Snapshot(
                remaining: 96, objectiveSeconds: 16, alphaFlag: (ObjectiveItemState.Dropped, null), bravoFlag: (ObjectiveItemState.Dropped, null)));

            var flags = (CaptureTheFlagStatus)status.Status.Detail;
            Assert.That(flags.GetReturnSeconds(Side.Alpha), Is.EqualTo(16f));
            Assert.That(flags.GetReturnSeconds(Side.Bravo), Is.EqualTo(20f));

            status.Apply(Snapshot(remaining: 80, objectiveSeconds: 4, bravoFlag: (ObjectiveItemState.Dropped, null)));

            flags = (CaptureTheFlagStatus)status.Status.Detail;
            Assert.That(flags.GetState(Side.Alpha), Is.EqualTo(FlagState.AtBase));
            Assert.That(flags.GetReturnSeconds(Side.Bravo), Is.EqualTo(4f), "a lone dropped flag reads the seconds as they are");
        }

        [Test]
        public void DropsSeenOnlyAfterJoiningUseTheSecondsAsTheyAre()
        {
            var status = new ClientCaptureTheFlagStatus(_local);

            status.Apply(Snapshot(
                remaining: 90, objectiveSeconds: 7, alphaFlag: (ObjectiveItemState.Dropped, null), bravoFlag: (ObjectiveItemState.Dropped, null)));

            var flags = (CaptureTheFlagStatus)status.Status.Detail;
            Assert.That(flags.GetReturnSeconds(Side.Alpha), Is.EqualTo(7f));
            Assert.That(flags.GetReturnSeconds(Side.Bravo), Is.EqualTo(7f));
        }

        [Test]
        public void TheEndOfTheMatchNamesTheWinningSide()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            var snapshot = Snapshot(alphaPoints: 1, bravoPoints: 3);
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = true;
            snapshot.Match.Winner = Team.Bravo;

            status.Apply(snapshot);

            Assert.That(status.Status.IsEnded, Is.True);
            Assert.That(status.Status.Winner, Is.EqualTo(Side.Bravo));
        }

        [Test]
        public void ADrawHasNoWinner()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            var snapshot = Snapshot();
            snapshot.Match.State = GameModeState.Ended;
            snapshot.Match.HasWinner = false;

            status.Apply(snapshot);

            Assert.That(status.Status.IsEnded, Is.True);
            Assert.That(status.Status.Winner, Is.Null);
        }

        [Test]
        public void WithoutBothFlagsTheScoresAreShownWithoutFlagDetail()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            var snapshot = Snapshot(alphaPoints: 2);
            snapshot.ObjectiveItems.Clear();

            status.Apply(snapshot);

            Assert.That(status.Status.Detail, Is.Null);
            Assert.That(status.Status.SideScores[0].Score, Is.EqualTo(2d));
        }

        [Test]
        public void APlayerMissingFromTheSnapshotHasNoSide()
        {
            var status = new ClientCaptureTheFlagStatus(99);

            status.Apply(Snapshot());

            Assert.That(status.Status.OwnSide, Is.Null);
            Assert.That(((CaptureTheFlagStatus)status.Status.Detail).LocalSide, Is.Null);
        }

        [Test]
        public void LeavingTheCaptureTheFlagMatchClearsTheStatus()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            status.Apply(Snapshot());
            var other = Snapshot();
            other.Match.Objective = MatchObjective.None;

            Assert.That(status.Apply(other), Is.True);
            Assert.That(status.Status, Is.Null);
            Assert.That(status.Apply(other), Is.False);
        }

        [Test]
        public void ClearForgetsTheStatus()
        {
            var status = new ClientCaptureTheFlagStatus(_local);
            status.Apply(Snapshot());

            Assert.That(status.Clear(), Is.True);
            Assert.That(status.Status, Is.Null);
            Assert.That(status.Clear(), Is.False);
            Assert.That(status.Apply(Snapshot()), Is.True, "the next snapshot publishes again");
        }

        private static SnapshotMessage Snapshot(
            int remaining = 120,
            int alphaPoints = 0,
            int bravoPoints = 0,
            int objectiveSeconds = 0,
            (ObjectiveItemState State, int? Carrier)? alphaFlag = null,
            (ObjectiveItemState State, int? Carrier)? bravoFlag = null)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            snapshot.Match.Objective = MatchObjective.CaptureTheFlag;
            snapshot.Match.RemainingSeconds = remaining;
            snapshot.Match.AlphaPoints = alphaPoints;
            snapshot.Match.BravoPoints = bravoPoints;
            snapshot.Match.ObjectiveRemainingSeconds = objectiveSeconds;
            AddPlayer(snapshot, _local, Team.Alpha);
            AddPlayer(snapshot, _enemy, Team.Bravo);
            AddFlag(snapshot, Team.Alpha, alphaFlag ?? (ObjectiveItemState.Home, null));
            AddFlag(snapshot, Team.Bravo, bravoFlag ?? (ObjectiveItemState.Home, null));
            return snapshot;
        }

        private static void AddPlayer(SnapshotMessage snapshot, int playerId, Team team)
        {
            ref var row = ref snapshot.Players.Add();
            row.PlayerId = playerId;
            row.Team = team;
            row.Health = 100;
        }

        private static void AddFlag(SnapshotMessage snapshot, Team team, (ObjectiveItemState State, int? Carrier) flag)
        {
            ref var item = ref snapshot.ObjectiveItems.Add();
            item.ItemId = team == Team.Alpha ? 0 : 1;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Flag;
            item.Team = team;
            item.Position = team == Team.Alpha ? new Vector3(-20f, 1f, 0f) : new Vector3(20f, 1f, 0f);
            item.State = flag.State;
            item.HasCarrier = flag.Carrier.HasValue;
            item.CarrierId = flag.Carrier ?? 0;
        }
    }
}
